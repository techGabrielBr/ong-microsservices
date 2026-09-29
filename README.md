# Conexão Solidária

Plataforma digital da ONG Esperança Solidária para gestão de campanhas e doações — MVP do Hackathon 11NETT.

Arquitetura de microsserviços em .NET 10, comunicação assíncrona por mensageria, persistência poliglota (PostgreSQL + DynamoDB via LocalStack), orquestração em Kubernetes, observabilidade com Prometheus/Grafana e pipeline de CI no GitHub Actions.

- [Diagrama e decisões de arquitetura](docs/arquitetura.md)
- [Justificativa dos bancos de dados](docs/decisao-bancos-de-dados.md)

## Sumário

- [O que foi construído](#o-que-foi-construído)
- [Pré-requisitos](#pré-requisitos)
- [Subindo tudo com Docker Compose](#subindo-tudo-com-docker-compose-caminho-recomendado)
- [Subindo no Kubernetes](#subindo-no-kubernetes)
- [Roteiro de demonstração](#roteiro-de-demonstração)
- [API](#api)
- [Postman](#postman)
- [Rodando sem containers](#rodando-sem-containers)
- [Observabilidade](#observabilidade)
- [Solução de problemas](#solução-de-problemas)
- [Testes](#testes)
- [Estrutura do repositório](#estrutura-do-repositório)

## O que foi construído

| Componente | Papel | Porta local |
|---|---|---|
| `ConexaoSolidaria.Api` | Autenticação JWT + RBAC, campanhas, cadastro de doador, painel público, recebimento de doações | 5080 |
| `ConexaoSolidaria.Worker` | Consome a fila e consolida o valor arrecadado das campanhas | 5081 |
| `ConexaoSolidaria.Gateway` | API Gateway com Ocelot (bônus) | 8080 |
| PostgreSQL | Usuários e campanhas | 5432 |
| DynamoDB (LocalStack) | Ledger de doações | 4566 |
| RabbitMQ | Broker de eventos + DLQ | 5672 / 15672 |
| Prometheus + Grafana | Coleta de métricas e dashboards | 9090 / 3000 |
| Zabbix (server + web + agente) | Monitoração e alertas sobre os serviços | 8090 |

**A regra central:** ao receber uma doação a API **não** atualiza o valor arrecadado. Ela grava a doação como `Pendente`, publica `DoacaoRecebidaEvent` no broker e responde `202 Accepted`. O Worker consome, aplica a transição idempotente no ledger e só então incrementa a campanha no PostgreSQL.

## Pré-requisitos

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (com Kubernetes habilitado, se for usar a rota K8s)
- [.NET SDK 10](https://dotnet.microsoft.com/download) — apenas para rodar fora de containers ou executar os testes
- `curl` e `python` (usados pelo script de smoke test em Bash) ou PowerShell 5.1+ (script `.ps1`)

## Subindo tudo com Docker Compose (caminho recomendado)

```bash
# na raiz do repositório
docker compose up -d --build
```

A primeira execução compila as imagens .NET e a do Grafana, e leva alguns minutos. A stack completa sobe 14 containers (incluindo a de monitoração Zabbix) e pede cerca de 6 GB de RAM disponíveis para o Docker. Acompanhe até tudo ficar saudável:

```bash
docker compose ps
docker compose logs -f api worker
```

A API está pronta quando o log mostrar `Etapa concluida: migrations Postgres`, `Etapa concluida: tabela DynamoDB` e `Etapa concluida: topologia de mensageria`. As migrations do EF Core, a tabela do DynamoDB, a exchange/fila do RabbitMQ e o usuário gestor inicial são criados automaticamente no startup — não há passo manual.

| Onde olhar | URL | Credenciais |
|---|---|---|
| **Entrada da aplicação (Gateway)** | **http://localhost:8080** | — |
| Swagger | http://localhost:8080/swagger | — |
| Acesso direto à API (depuração) | http://localhost:5080/swagger | — |
| RabbitMQ Management | http://localhost:15672 | `guest` / `guest` |
| Prometheus | http://localhost:9090/targets | — |
| Grafana | http://localhost:3000 | `admin` / `admin` |
| Zabbix | http://localhost:8090 | `Admin` / `zabbix` |
| Métricas brutas | http://localhost:5080/metrics · http://localhost:5081/metrics | — |

Gestor criado automaticamente: **`gestor@conexaosolidaria.org`** / **`Gestor@123`**.

Validando o fluxo inteiro de uma vez:

```bash
./scripts/smoke-test.sh http://localhost:8080      # Bash, pelo gateway
```
```powershell
.\scripts\smoke-test.ps1 -BaseUrl http://localhost:8080
```

O script faz login como gestor, cria uma campanha, cadastra um doador, envia uma doação e aguarda o Worker consolidar — falhando se o valor não bater no painel público.

Derrubando:

```bash
docker compose down          # mantém o volume do Postgres
docker compose down -v       # zera tudo
```

### Trocando o broker para AWS (SNS/SQS no LocalStack)

O broker é plugável. Para rodar sobre SNS + SQS em vez de RabbitMQ, sem alterar código:

```bash
# api e worker precisam do mesmo provider
docker compose stop api worker
Messaging__Provider=Aws docker compose up -d api worker
```

Ou fixe `Messaging__Provider: Aws` no `docker-compose.yml`. Para inspecionar os recursos criados no LocalStack:

```bash
docker exec cs-localstack awslocal sqs list-queues
docker exec cs-localstack awslocal sns list-topics
docker exec cs-localstack awslocal dynamodb scan --table-name doacoes --max-items 5
```

## Subindo no Kubernetes

Funciona com Docker Desktop K8s, Minikube ou Kind. As imagens precisam existir localmente com as tags que os manifests esperam.

```bash
# 1. construir as imagens
docker build -f deploy/Dockerfile.api     -t conexao-solidaria/api:latest .
docker build -f deploy/Dockerfile.worker  -t conexao-solidaria/worker:latest .
docker build -f deploy/Dockerfile.gateway -t conexao-solidaria/gateway:latest .
docker build -f deploy/Dockerfile.grafana -t conexao-solidaria/grafana:11.2.0 .

# 2. Minikube/Kind precisam receber as imagens (Docker Desktop K8s já as enxerga)
minikube image load conexao-solidaria/api:latest conexao-solidaria/worker:latest conexao-solidaria/gateway:latest conexao-solidaria/grafana:11.2.0
# kind load docker-image conexao-solidaria/api:latest conexao-solidaria/worker:latest conexao-solidaria/gateway:latest conexao-solidaria/grafana:11.2.0

# 3. aplicar os manifests na ordem numérica
kubectl apply -f deploy/k8s/

# 4. acompanhar
kubectl get pods -n conexao-solidaria -w
```

Todos os pods `Running` e `READY`:

```bash
kubectl get pods,svc -n conexao-solidaria
```

| Serviço | Acesso |
|---|---|
| **Gateway (entrada)** | **http://localhost:30081** |
| API direta (depuração) | http://localhost:30080/swagger |
| RabbitMQ Management | http://localhost:31567 |
| Prometheus | http://localhost:30090 |
| Grafana | http://localhost:30300 (`admin`/`admin`) |
| Zabbix | http://localhost:30909 (`Admin`/`zabbix`) |

> No Minikube use `minikube service <nome> -n conexao-solidaria` ou `minikube ip` no lugar de `localhost`.

Rodando o smoke test contra o cluster:

```bash
./scripts/smoke-test.sh http://localhost:30081   # gateway
```

Removendo:

```bash
kubectl delete -f deploy/k8s/
```

## Roteiro de demonstração

Sequência sugerida para o vídeo, provando cada requisito. Com o ambiente no ar:

**1. Autenticação e obtenção do token**

```bash
curl -X POST http://localhost:5080/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"gestor@conexaosolidaria.org","senha":"Gestor@123"}'
```

Guarde o `accessToken`. No Swagger, clique em **Authorize** e cole apenas o token.

**2. RBAC funcionando** — tente criar uma campanha sem token (`401`) e depois com um token de doador (`403`).

**3. Criar uma campanha (GestorONG)**

```bash
TOKEN=<accessToken do gestor>
curl -X POST http://localhost:5080/api/v1/campanhas \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{
    "titulo": "Campanha do Agasalho 2026",
    "descricao": "Arrecadação para o inverno das crianças acolhidas",
    "dataInicio": "2026-09-05T00:00:00Z",
    "dataFim": "2026-12-20T23:59:59Z",
    "metaFinanceira": 50000
  }'
```

Mostre também a regra de negócio recusando `dataFim` no passado ou `metaFinanceira: 0` (`422`).

**4. Cadastrar um doador (público)**

```bash
curl -X POST http://localhost:5080/api/v1/auth/doadores \
  -H 'Content-Type: application/json' \
  -d '{"nomeCompleto":"Ana Paula Ribeiro","email":"ana@exemplo.com","cpf":"529.982.247-25","senha":"Doador@123"}'
```

CPF inválido e e-mail repetido são recusados — vale demonstrar.

**5. Painel público antes da doação** — repare no `valorTotalArrecadado: 0`:

```bash
curl http://localhost:5080/api/v1/publico/campanhas
```

**6. Enviar a doação** (token do doador, `202 Accepted`, status `Pendente`):

```bash
curl -X POST http://localhost:5080/api/v1/doacoes \
  -H "Authorization: Bearer $TOKEN_DOADOR" -H 'Content-Type: application/json' \
  -d '{"idCampanha":"<id-da-campanha>","valorDoacao":1500.50}'
```

**7. A mensagem no broker** — abra http://localhost:15672 → *Queues* → `doacoes.recebidas` e mostre o gráfico de mensagens publicadas/entregues, e a exchange `conexao-solidaria.doacoes` em *Exchanges*.

**8. O Worker consolidando** — `docker compose logs -f worker` mostra `Doacao {id} de R$ 1.500,50 consolidada na campanha {id}`.

**9. Painel público depois** — o mesmo `GET /api/v1/publico/campanhas` agora traz `valorTotalArrecadado: 1500.50`, atualizado **pelo Worker**, não pela API.

**10. Grafana** — http://localhost:3000 → dashboard *Conexão Solidária - Plataforma*. O painel "Doações recebidas x processadas" mostra as duas curvas, e há CPU/memória dos containers e tráfego HTTP por endpoint.

**11. Zabbix** — http://localhost:8090 (`Admin`/`zabbix`) → *Monitoring* → *Latest data*, filtrando pelo host group **Conexão Solidária**: os contadores de doações lidos direto do `/metrics` de cada serviço. Em *Monitoring* → *Problems*, pare o Worker (`docker compose stop worker`) e mostre a trigger **"Worker fora do ar"** subindo como *Disaster* em ~3 minutos; ao religar, o problema resolve sozinho.

**12. Pods no Kubernetes** — `kubectl get pods -n conexao-solidaria`.

**13. Pipeline** — aba *Actions* do GitHub: build, testes xUnit e as três imagens Docker publicadas no GHCR.

Para gerar carga contínua durante a gravação (várias doações seguidas):

```bash
for i in $(seq 1 20); do
  curl -s -X POST http://localhost:5080/api/v1/doacoes \
    -H "Authorization: Bearer $TOKEN_DOADOR" -H 'Content-Type: application/json' \
    -d "{\"idCampanha\":\"<id>\",\"valorDoacao\":$((RANDOM % 500 + 10))}" > /dev/null
done
```

## API

Base: **`http://localhost:8080`** — todo o tráfego de cliente entra pelo API Gateway (Ocelot), que roteia `/api/*` e `/swagger/*` para a API. O acesso direto em `:5080` continua publicado apenas para depuração.

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| `POST` | `/api/v1/auth/doadores` | Público | Cadastro de doador (e-mail único, CPF validado, senha em BCrypt) |
| `POST` | `/api/v1/auth/login` | Público | Retorna o token JWT |
| `GET` | `/api/v1/publico/campanhas` | Público | **Painel de transparência**: campanhas `Ativa` com título, meta e total arrecadado |
| `GET` | `/api/v1/publico/campanhas/{id}` | Público | Detalhe público de uma campanha ativa |
| `GET` | `/api/v1/campanhas` | GestorONG | Todas as campanhas |
| `POST` | `/api/v1/campanhas` | GestorONG | Cria campanha |
| `GET` | `/api/v1/campanhas/{id}` | GestorONG | Detalha campanha |
| `PUT` | `/api/v1/campanhas/{id}` | GestorONG | Edita campanha |
| `POST` | `/api/v1/campanhas/{id}/cancelar` | GestorONG | Cancela |
| `POST` | `/api/v1/campanhas/{id}/concluir` | GestorONG | Conclui |
| `POST` | `/api/v1/doacoes` | Doador | Intenção de doação → publica o evento (`202`) |
| `GET` | `/api/v1/doacoes/minhas` | Autenticado | Histórico do doador (DynamoDB, GSI `doador-index`) |
| `GET` | `/api/v1/doacoes/campanha/{id}` | GestorONG | Extrato de doações da campanha |
| `GET` | `/api-health` · `/api-live` | Público | Saúde da API, pelo gateway |
| `GET` | `/worker-health` · `/worker-live` | Público | Saúde do Worker, pelo gateway |
| `GET` | `/api-metrics` · `/worker-metrics` | Público | Métricas Prometheus de cada serviço, pelo gateway |
| `GET` | `/health/live` · `/metrics` | Público | Saúde e métricas do **próprio gateway** |

> O scrape que o Prometheus e o Zabbix fazem continua indo **direto** a `api:8080/metrics` e `worker:8080/metrics` pela rede interna. Passar coleta de métricas por gateway adicionaria um ponto de falha e distorceria a latência medida de cada serviço.

Regras de negócio aplicadas no domínio:

- campanha não pode ser criada com data de término no passado;
- meta financeira deve ser maior que zero;
- doação é recusada (`422`) para campanha `Cancelada`, `Concluida` ou já encerrada;
- e-mail e CPF são únicos; CPF é validado por dígito verificador;
- senha nunca é persistida em claro — BCrypt com work factor 11.

## Postman

A pasta `postman/` traz a coleção completa, com **30 requisições** e **60 assertions** cobrindo cada requisito funcional do enunciado.

```
postman/
├── conexao-solidaria.postman_collection.json       a coleção
├── conexao-solidaria.postman_environment.json      ambiente local (Docker Compose)
└── conexao-solidaria-k8s.postman_environment.json  ambiente Kubernetes (NodePort)
```

No Postman: **Import** → arraste os três arquivos → selecione o ambiente **Conexão Solidária · Local** no canto superior direito.

**Tokens e IDs são capturados sozinhos.** O login salva `{{tokenGestor}}`, a criação de campanha salva `{{campanhaId}}`, e assim por diante — não há nada para copiar entre requisições. Se você abrir uma requisição do meio da coleção sem ter rodado as anteriores, um script de preparação cria o que faltar (token, doador, campanha) antes de enviar; qualquer requisição funciona isolada.

As pastas seguem a ordem da demonstração:

| Pasta | O que cobre |
|---|---|
| `00 · Saúde da plataforma` | API e Worker respondendo |
| `01 · Autenticação e RBAC` | Login, cadastro de doador, 401 sem token, 403 com token de doador, CPF inválido, e-mail duplicado |
| `02 · Campanhas` | Criação, edição, listagem e as regras de data no passado e meta zero |
| `03 · Painel de transparência` | Rota pública e o total arrecadado **antes** da doação |
| `04 · Doação assíncrona` | O núcleo: `202 Accepted`, o valor consolidado **depois** pelo Worker, histórico no ledger e as regras de recusa |
| `05 · Observabilidade` | `/metrics` da API e do Worker, com os contadores de negócio |
| `06 · API Gateway` | Ocelot roteando (bônus) |

**Para a demonstração ao vivo**, o caminho curto é: `01 · Login do Gestor` → `02 · Criar campanha` → `03 · ANTES da doação` → `04 · Enviar doação` → `04 · DEPOIS da doação`. Entre as duas últimas, mostre a mensagem na fila do RabbitMQ e o log do Worker consumindo.

**Para rodar tudo de uma vez**, use o Collection Runner — ou, pela linha de comando:

```bash
npx newman run postman/conexao-solidaria.postman_collection.json   -e postman/conexao-solidaria.postman_environment.json --delay-request 400
```

A requisição *DEPOIS da doação* repete sozinha até o Worker consolidar (até 10 tentativas), então a coleção passa inteira sem intervenção.

## Rodando sem containers

Útil para depurar. Suba apenas a infraestrutura e rode as aplicações pelo SDK:

```bash
docker compose up -d postgres rabbitmq localstack

dotnet run --project src/ConexaoSolidaria.Api      # http://localhost:5080/swagger
dotnet run --project src/ConexaoSolidaria.Worker   # em outro terminal
```

Os `appsettings.json` já apontam para `localhost` nas portas padrão.

Gerando uma nova migration depois de alterar entidades:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Nome> \
  --project src/ConexaoSolidaria.Infrastructure \
  --startup-project src/ConexaoSolidaria.Api \
  --output-dir Persistence/Migrations
```

## Observabilidade

Todos os serviços expõem `/health/live`, `/health/ready` e `/metrics` no formato Prometheus.

**Métricas de negócio** (`MetricasDoacao`), que provam o fluxo assíncrono em números:

| Métrica | Origem | O que mostra |
|---|---|---|
| `conexao_solidaria_doacoes_recebidas_total` | API | Intenções aceitas e publicadas no broker |
| `conexao_solidaria_doacoes_processadas_total` | Worker | Doações consolidadas na campanha |
| `conexao_solidaria_doacoes_rejeitadas_total{motivo}` | API | Recusas por regra de negócio, por motivo |
| `conexao_solidaria_doacoes_duplicadas_total` | Worker | Reentregas do broker descartadas pela idempotência |
| `conexao_solidaria_doacoes_falhas_total` | Worker | Falhas que resultaram em DLQ |
| `conexao_solidaria_doacao_processamento_segundos` | Worker | Histograma de latência do consumo |
| `conexao_solidaria_valor_arrecadado_total` | Worker | Valor consolidado desde o start |

Somam-se a essas as métricas HTTP (`http_requests_received_total`, `http_request_duration_seconds`) e de runtime (`process_cpu_seconds_total`, `process_working_set_bytes`, `dotnet_total_memory_bytes`).

O dashboard **Conexão Solidária - Plataforma** é provisionado automaticamente no Grafana (Compose e Kubernetes) com 11 painéis: recebidas × processadas, backlog em trânsito, throughput, latência p50/p95, tráfego HTTP por endpoint e por status, CPU e memória por serviço, falhas e duplicidades.

### Zabbix

O Zabbix monitora os três microsserviços e é provisionado automaticamente: o container `zabbix-provisioner` chama a API JSON-RPC e cria host group, hosts, itens e triggers (`deploy/zabbix/provisionar.py`). O script é idempotente — rodar de novo atualiza as definições em vez de duplicar.

A coleta **não usa agente dentro dos pods da aplicação**. Cada host tem um item HTTP agent que lê o `/metrics` do serviço, e os demais itens derivam dele por *preprocessing* com o padrão Prometheus nativo do Zabbix. Um único request de 30 em 30 segundos alimenta todos os itens daquele serviço.

| Host no Zabbix | Itens | Triggers |
|---|---|---|
| Conexão Solidária API | health, doações recebidas (total e por segundo), requisições HTTP, erros 5xx, CPU, memória, memória .NET | API fora do ar (*desastre*), memória acima de 700MB, erro 5xx |
| Conexão Solidária Worker | health, doações processadas (total e por segundo), reentregas ignoradas, falhas/DLQ, valor consolidado, CPU, memória | Worker fora do ar (*desastre*), doações caindo na DLQ, backlog não consolidado |
| Conexão Solidária Gateway | health, memória | Gateway fora do ar |

A trigger de **backlog** cruza os dois hosts — `doacoes.recebidas` da API menos `doacoes.processadas` do Worker — e alerta quando o consumidor não está drenando a fila no ritmo em que a API aceita doações. É o alerta que traduz a arquitetura assíncrona em operação.

Para demonstrar um alerta disparando ao vivo:

```bash
docker compose stop worker          # aguarde ~3 min
# Zabbix → Monitoring → Problems: "Conexao Solidaria: Worker fora do ar" (Disaster)
docker compose start worker         # o problema resolve sozinho na coleta seguinte
```

**Zabbix e Grafana juntos:** o Grafana tem o plugin `alexanderzobnin-zabbix-app` instalado e o Zabbix provisionado como datasource, então há dois dashboards na pasta *Conexão Solidária*:

| Dashboard | Fonte | Foco |
|---|---|---|
| Conexão Solidária - Plataforma | Prometheus | Séries temporais detalhadas, latência p50/p95, throughput |
| Conexão Solidária - Zabbix | Zabbix | Status dos serviços, painel de problemas ativos, métricas de negócio e alertas |

Prometheus e Zabbix leem exatamente os mesmos endpoints `/metrics` — não há instrumentação duplicada na aplicação.

## Solução de problemas

**`dotnet restore` falha no build da imagem com `NU1301: The SSL connection could not be established`**
Antivírus com "SSL scanning" (Avast, Kaspersky) ou proxy corporativo reassinam o HTTPS com uma CA que o container não conhece. Exporte essa CA para `deploy/certs/*.crt` — os Dockerfiles a instalam automaticamente antes do restore. Passo a passo em [`deploy/certs/README.md`](deploy/certs/README.md).

**Grafana não abre em `localhost:3000`**
A porta pode já estar em uso na sua máquina (outro Grafana, um dev server). Confirme com `docker port cs-grafana`: se não retornar nada, o bind não aconteceu. Troque a porta publicada no `docker-compose.yml` (ex.: `"3001:3000"`).

**Painéis de CPU/memória vazios**
Os painéis usam as métricas de processo dos próprios serviços, então funcionam em qualquer ambiente. O container `cadvisor` é um extra para métricas por container e não funciona no Docker Desktop para Windows (ele não consegue falar com o daemon); isso não afeta o dashboard.

**A API sobe mas fica repetindo `Falha na etapa ...`**
É o retry de inicialização esperando PostgreSQL, LocalStack ou RabbitMQ ficarem prontos. Confirme com `docker compose ps` que os três estão `healthy`.

**Worker não consolida o valor**
Verifique se API e Worker estão com o **mesmo** `Messaging__Provider`. Com providers diferentes a API publica em um broker e o Worker escuta outro. Veja a fila em http://localhost:15672 → *Queues* → `doacoes.recebidas`.

**Mensagens acumulando em `doacoes.recebidas.dlq`**
São eventos que falharam no processamento (payload inválido ou campanha inexistente). `docker compose logs worker` mostra a exceção de cada uma.

## Testes

```bash
dotnet test
```

37 testes de unidade cobrindo as regras de domínio: validação de campanha (datas, meta, transições de status), elegibilidade para doação, validação de CPF por dígito verificador, normalização de e-mail, arredondamento monetário e regras da doação. Rodam na esteira de CI a cada push.

## Estrutura do repositório

```
├── src/
│   ├── ConexaoSolidaria.Domain/          entidades, regras de negócio, value objects (CPF, Email)
│   ├── ConexaoSolidaria.Contracts/       contratos de evento compartilhados entre os serviços
│   ├── ConexaoSolidaria.Infrastructure/  EF Core, repositórios, ledger DynamoDB, RabbitMQ/SNS-SQS, JWT, métricas
│   ├── ConexaoSolidaria.Api/             microsserviço HTTP (minimal APIs)
│   ├── ConexaoSolidaria.Worker/          microsserviço consumidor
│   └── ConexaoSolidaria.Gateway/         API Gateway (Ocelot)
├── tests/ConexaoSolidaria.Domain.Tests/  xUnit + FluentAssertions
├── deploy/
│   ├── Dockerfile.{api,worker,gateway}
│   ├── certs/                            CAs extras para redes com inspeção TLS (opcional)
│   ├── k8s/                              Namespace, ConfigMap, Secret, Deployments, Services
│   ├── localstack/                       provisionamento de DynamoDB/SNS/SQS
│   ├── prometheus/                       configuração de scrape
│   ├── grafana/                          datasources e dashboards provisionados
│   └── zabbix/                           provisionamento de hosts, itens e triggers via API
├── docs/                                 arquitetura e decisão dos bancos
├── postman/                              coleção e ambientes do Postman
├── scripts/                              smoke test end-to-end (bash e PowerShell) e gerador de CPF
├── .github/workflows/ci.yml              build, testes e imagens Docker
└── docker-compose.yml
```
