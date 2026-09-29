namespace ConexaoSolidaria.Contracts.Events;

public static class MessagingTopics
{
    public const string Exchange = "conexao-solidaria.doacoes";
    public const string RoutingKeyDoacaoRecebida = "doacao.recebida";
    public const string FilaDoacoesRecebidas = "doacoes.recebidas";
    public const string FilaDoacoesDlq = "doacoes.recebidas.dlq";
}
