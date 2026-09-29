# Justificativa da escolha dos bancos de dados

> Documento exigido no item 2.b dos entregáveis: por que os bancos X e Y foram escolhidos.
> Para entregar em PDF: `pandoc docs/decisao-bancos-de-dados.md -o decisao-bancos-de-dados.pdf`
> (ou imprimir a visualização do Markdown para PDF pelo navegador).

## Resumo

| | Banco X | Banco Y |
|---|---|---|
| Produto | **PostgreSQL 16** (relacional) | **Amazon DynamoDB** (NoSQL chave-valor/documento), emulado por **LocalStack** |
| Guarda | `usuarios`, `campanhas` | Ledger de doações (`doacoes`) |
| Serviço dono | `ConexaoSolidaria.Api` escreve; o Worker só faz o incremento atômico do valor arrecadado | API grava a doação `Pendente`; Worker faz a transição para `Processada` |
| Padrão de acesso | Consultas por status, unicidade de e-mail/CPF, agregação de valores | Escritas de alto volume, leitura sempre por chave conhecida |

A plataforma usa **persistência poliglota**: cada banco resolve o problema em que é melhor, em vez de forçar um único produto a atender requisitos opostos.

## Banco X — PostgreSQL para identidade e campanhas

**O que esses dados exigem:**

1. **Unicidade forte.** O requisito 3 pede e-mail único e CPF válido. Um índice `UNIQUE` no PostgreSQL resolve isso no próprio banco — não depende de o código conferir antes de inserir. Duas requisições simultâneas com o mesmo e-mail: uma passa, a outra recebe violação de constraint.
2. **Transações ACID.** Criar campanha, editar e transicionar status são operações que precisam ser atômicas e consistentes de imediato — o painel público não pode mostrar uma campanha pela metade.
3. **Consultas por atributo não-chave.** `WHERE Status = 'Ativa' ORDER BY CriadaEm` é a consulta central do painel de transparência. Em relacional é um índice comum; em chave-valor exigiria índice secundário e modelagem extra.
4. **Incremento atômico do valor arrecadado.** `UPDATE campanhas SET ValorArrecadado = ValorArrecadado + @valor WHERE Id = @id` é resolvido pelo banco, com bloqueio de linha. Isso permite rodar **várias réplicas do Worker em paralelo** sem perda de escrita e sem lock distribuído na aplicação.
5. **Volume pequeno e previsível.** Dezenas de campanhas e milhares de usuários cabem folgadamente em uma instância — não há razão para pagar a complexidade de um NoSQL aqui.

**Por que não Mongo/DynamoDB para esse conjunto:** perderíamos unicidade garantida pelo banco, o incremento atômico ficaria a cargo da aplicação (ou de expressões condicionais com retry) e as consultas por status exigiriam índices secundários com consistência eventual — justamente onde o painel precisa de número correto.

## Banco Y — DynamoDB (LocalStack) para o ledger de doações

**O que esses dados exigem:**

1. **Volume de escrita desproporcional.** Doações são o dado que mais cresce: uma campanha viral gera milhares de registros em minutos, enquanto o número de campanhas continua igual. DynamoDB escala escrita horizontalmente por partição, sem tuning de instância.
2. **Acesso sempre por chave.** Toda leitura de doação é "as doações desta campanha" ou "as minhas doações". A modelagem sai direto disso:
   - Partition key `CampanhaId` + sort key `DoacaoId` → `Query` do extrato de uma campanha em uma única partição;
   - GSI `doador-index` (`DoadorId` + `CriadaEm`) → histórico do doador já ordenado por data.
   Nenhuma consulta do produto precisa de `JOIN` ou varredura — o caso de uso ideal para chave-valor.
3. **Escrita condicional para idempotência.** O ponto decisivo. O broker entrega *at-least-once*; a mesma `DoacaoRecebidaEvent` pode chegar duas vezes ao Worker. A transição usa:

   ```
   UpdateExpression:    SET #status = :processada
   ConditionExpression: attribute_exists(DoacaoId) AND #status = :pendente
   ```

   Só a primeira entrega satisfaz a condição; a segunda recebe `ConditionalCheckFailedException`, é contabilizada como duplicidade e descartada — **sem creditar o valor duas vezes na campanha**. Idempotência garantida pelo banco, não por uma trava na aplicação.
4. **Registro imutável e append-only.** O ledger é histórico financeiro: escreve uma vez, muda de status uma vez, nunca é atualizado em massa. Não há relacionamento a preservar — é exatamente o formato documento.
5. **Retenção barata e crescimento sem migração.** Não há schema a versionar quando a doação ganhar um campo novo (meio de pagamento, recibo fiscal), e TTL nativo permitiria arquivar registros antigos sem job próprio.

**Por que não guardar doações no PostgreSQL:** a tabela viraria o hotspot de escrita do sistema, competindo com as leituras do painel público na mesma instância. E a idempotência exigiria `INSERT ... ON CONFLICT` com controle de estado manual — possível, mas acoplando o histórico financeiro ao banco transacional que já é a fonte da verdade das campanhas.

## Como os dois se articulam

O valor arrecadado existe em dois lugares com papéis diferentes, e isso é intencional:

- **DynamoDB é a fonte da verdade** do que aconteceu: cada doação individual, seu status e seu horário. É auditável e reconstruível.
- **PostgreSQL guarda o total consolidado**, um valor derivado, mantido para que o painel público responda em uma única leitura indexada — sem varrer milhares de doações a cada acesso.

O Worker é o único componente que atravessa a fronteira, e sempre na ordem segura: primeiro a escrita condicional no DynamoDB (que decide se este evento já foi processado), depois o incremento no PostgreSQL. Se o processo morrer entre os dois passos, a mensagem não recebe `ack`, é reentregue, a condição no DynamoDB falha e a doação fica registrada como `Processada` sem o crédito correspondente — divergência detectável comparando a soma do ledger com o total consolidado, e corrigível por reprocessamento. É a escolha consciente de um sistema *at-least-once* com idempotência: preferimos uma divergência rara e auditável a creditar valor em duplicidade.

## LocalStack

O DynamoDB roda no LocalStack (`http://localhost:4566`) em desenvolvimento, no Docker Compose e no cluster Kubernetes — mesma API, mesmo SDK, mesmo código. Migrar para a AWS real é remover `Aws:ServiceUrl` da configuração: o `AmazonDynamoDBClient` passa a usar o endpoint regional e a cadeia de credenciais padrão. Nenhuma linha de aplicação muda.
