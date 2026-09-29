using Prometheus;

namespace ConexaoSolidaria.Infrastructure.Observabilidade;

public static class MetricasDoacao
{
    public static readonly Counter Recebidas = Metrics.CreateCounter(
        "conexao_solidaria_doacoes_recebidas_total",
        "Intencoes de doacao aceitas pela API e publicadas no broker.");

    public static readonly Counter Rejeitadas = Metrics.CreateCounter(
        "conexao_solidaria_doacoes_rejeitadas_total",
        "Intencoes de doacao recusadas por regra de negocio.",
        new CounterConfiguration { LabelNames = ["motivo"] });

    public static readonly Counter Processadas = Metrics.CreateCounter(
        "conexao_solidaria_doacoes_processadas_total",
        "Doacoes consumidas da fila e consolidadas na campanha pelo Worker.");

    public static readonly Counter Duplicadas = Metrics.CreateCounter(
        "conexao_solidaria_doacoes_duplicadas_total",
        "Mensagens reentregues que ja haviam sido processadas (idempotencia).");

    public static readonly Counter FalhasProcessamento = Metrics.CreateCounter(
        "conexao_solidaria_doacoes_falhas_total",
        "Falhas de processamento no Worker que resultaram em nack/DLQ.");

    public static readonly Histogram DuracaoProcessamento = Metrics.CreateHistogram(
        "conexao_solidaria_doacao_processamento_segundos",
        "Tempo de processamento de uma doacao pelo Worker.");

    public static readonly Gauge ValorArrecadadoTotal = Metrics.CreateGauge(
        "conexao_solidaria_valor_arrecadado_total",
        "Valor consolidado creditado em campanhas desde a inicializacao do Worker.");
}
