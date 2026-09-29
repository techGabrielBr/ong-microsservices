"""
Variaveis de ambiente:
  ZABBIX_URL       default http://zabbix-web:8080/api_jsonrpc.php
  ZABBIX_USER      default Admin
  ZABBIX_PASSWORD  default zabbix
"""

import json
import os
import sys
import time
import urllib.error
import urllib.request

URL = os.environ.get("ZABBIX_URL", "http://zabbix-web:8080/api_jsonrpc.php")
USUARIO = os.environ.get("ZABBIX_USER", "Admin")
SENHA = os.environ.get("ZABBIX_PASSWORD", "zabbix")

GRUPO = "Conexao Solidaria"

# Tipos da API do Zabbix
ITEM_HTTP_AGENT = 19
ITEM_DEPENDENTE = 18
VALOR_TEXTO = 4
VALOR_FLOAT = 0
VALOR_INTEIRO = 3
PRE_PROMETHEUS = 22
PRE_JAVASCRIPT = 21

_token = None
_id = 0


def chamar(metodo, parametros, autenticar=True):
    global _id
    _id += 1
    corpo = {"jsonrpc": "2.0", "method": metodo, "params": parametros, "id": _id}
    if autenticar and _token:
        corpo["auth"] = _token

    requisicao = urllib.request.Request(
        URL,
        data=json.dumps(corpo).encode(),
        headers={"Content-Type": "application/json-rpc", "Authorization": f"Bearer {_token}" if _token else ""},
    )

    with urllib.request.urlopen(requisicao, timeout=30) as resposta:
        dados = json.load(resposta)

    if "error" in dados:
        raise RuntimeError(f"{metodo}: {dados['error'].get('data') or dados['error'].get('message')}")

    return dados["result"]


def esperar_api(tentativas=60):
    for i in range(1, tentativas + 1):
        try:
            versao = chamar("apiinfo.version", {}, autenticar=False)
            print(f"[zabbix-init] API respondendo, versao {versao}")
            return versao
        except Exception as erro:
            print(f"[zabbix-init] aguardando a API ({i}/{tentativas}): {erro}")
            time.sleep(5)
    raise SystemExit("[zabbix-init] API do Zabbix nao respondeu a tempo")


def autenticar():
    global _token
    _token = chamar("user.login", {"username": USUARIO, "password": SENHA}, autenticar=False)
    print("[zabbix-init] autenticado")


def garantir_grupo():
    existentes = chamar("hostgroup.get", {"filter": {"name": [GRUPO]}})
    if existentes:
        return existentes[0]["groupid"]
    return chamar("hostgroup.create", {"name": GRUPO})["groupids"][0]


def garantir_host(nome, grupoid, tags):
    existentes = chamar("host.get", {"filter": {"host": [nome]}})
    if existentes:
        print(f"[zabbix-init] host {nome} ja existe")
        return existentes[0]["hostid"]

    hostid = chamar("host.create", {
        "host": nome,
        "name": nome,
        "groups": [{"groupid": grupoid}],
        "tags": [{"tag": k, "value": v} for k, v in tags.items()],
    })["hostids"][0]
    print(f"[zabbix-init] host {nome} criado")
    return hostid


def garantir_item(hostid, chave, definicao):
    existentes = chamar("item.get", {"hostids": hostid, "filter": {"key_": [chave]}})

    if existentes:
        itemid = existentes[0]["itemid"]
        chamar("item.update", {**definicao, "itemid": itemid})
        return itemid

    return chamar("item.create", {**definicao, "hostid": hostid, "key_": chave})["itemids"][0]


def garantir_trigger(descricao, expressao, severidade, comentario=""):
    existentes = chamar("trigger.get", {"filter": {"description": [descricao]}})
    if existentes:
        return existentes[0]["triggerid"]

    return chamar("trigger.create", {
        "description": descricao,
        "expression": expressao,
        "priority": severidade,
        "comments": comentario,
        "manual_close": 1,
    })["triggerids"][0]


def item_metricas(hostid, url):
    return garantir_item(hostid, "prometheus.raw", {
        "name": "Endpoint /metrics (coleta base)",
        "type": ITEM_HTTP_AGENT,
        "value_type": VALOR_TEXTO,
        "url": url,
        "delay": "30s",
        "history": "0",
        "timeout": "10s",
    })


def item_prometheus(hostid, masterid, chave, nome, padrao, unidade="", tipo=VALOR_FLOAT,
                    taxa=False, agregacao=None, zero_se_ausente=False):
    parametros = f"{padrao}\nfunction\n{agregacao}" if agregacao else f"{padrao}\nvalue\n"
    tratamento = (2, "0") if zero_se_ausente else (0, "")

    definicao = {
        "name": nome,
        "type": ITEM_DEPENDENTE,
        "value_type": tipo,
        "master_itemid": masterid,
        "units": unidade,
        "history": "7d",
        "trends": "90d",
        "preprocessing": [
            {"type": PRE_PROMETHEUS, "params": parametros,
             "error_handler": tratamento[0], "error_handler_params": tratamento[1]},
        ],
    }
    if taxa:
        definicao["preprocessing"].append({"type": 10, "params": "", "error_handler": 0, "error_handler_params": ""})
    return garantir_item(hostid, chave, definicao)


def item_saude(hostid, url):
    return garantir_item(hostid, "health.ready", {
        "name": "Health check (/health/ready)",
        "type": ITEM_HTTP_AGENT,
        "value_type": VALOR_INTEIRO,
        "url": url,
        "delay": "30s",
        "timeout": "5s",
        "status_codes": "",
        "preprocessing": [
            # 1 quando o servico responde Healthy, 0 em qualquer outro caso
            {"type": PRE_JAVASCRIPT, "params": "return value.indexOf('Healthy') >= 0 ? 1 : 0;",
             "error_handler": 0, "error_handler_params": ""},
        ],
    })


def provisionar_api(grupoid):
    hostid = garantir_host("Conexao Solidaria API", grupoid, {"servico": "api", "camada": "aplicacao"})
    item_saude(hostid, "http://api:8080/health/ready")
    master = item_metricas(hostid, "http://api:8080/metrics")

    item_prometheus(hostid, master, "doacoes.recebidas", "Doacoes recebidas (total)",
                    "conexao_solidaria_doacoes_recebidas_total", tipo=VALOR_FLOAT)
    item_prometheus(hostid, master, "doacoes.recebidas.taxa", "Doacoes recebidas por segundo",
                    "conexao_solidaria_doacoes_recebidas_total", unidade="dps", taxa=True)
    item_prometheus(hostid, master, "http.requisicoes", "Requisicoes HTTP bem-sucedidas (total)",
                    'http_requests_received_total{code="200"}', agregacao="sum")
    item_prometheus(hostid, master, "http.requisicoes.taxa", "Requisicoes HTTP por segundo",
                    "http_requests_received_total", unidade="rps", taxa=True, agregacao="sum")
    item_prometheus(hostid, master, "http.erros", "Respostas HTTP 5xx (total)",
                    'http_requests_received_total{code=~"5.."}', agregacao="sum", zero_se_ausente=True)
    item_prometheus(hostid, master, "processo.memoria", "Memoria do processo",
                    "process_working_set_bytes", unidade="B")
    item_prometheus(hostid, master, "processo.cpu", "CPU do processo",
                    "process_cpu_seconds_total", unidade="%", taxa=True)
    item_prometheus(hostid, master, "dotnet.memoria", "Memoria gerenciada .NET",
                    "dotnet_total_memory_bytes", unidade="B")

    garantir_trigger(
        "Conexao Solidaria: API fora do ar",
        "last(/Conexao Solidaria API/health.ready)=0 or nodata(/Conexao Solidaria API/health.ready,3m)=1",
        5,
        "O endpoint /health/ready da API parou de responder Healthy.")

    garantir_trigger(
        "Conexao Solidaria: memoria da API acima de 700MB",
        "last(/Conexao Solidaria API/processo.memoria)>700000000",
        3,
        "Possivel vazamento de memoria ou carga acima do dimensionado.")

    garantir_trigger(
        "Conexao Solidaria: API retornando erro 5xx",
        "change(/Conexao Solidaria API/http.erros)>0",
        4,
        "A API passou a responder com erro interno em alguma rota.")

    return hostid


def provisionar_worker(grupoid):
    hostid = garantir_host("Conexao Solidaria Worker", grupoid, {"servico": "worker", "camada": "aplicacao"})
    item_saude(hostid, "http://worker:8080/health/ready")
    master = item_metricas(hostid, "http://worker:8080/metrics")

    item_prometheus(hostid, master, "doacoes.processadas", "Doacoes processadas (total)",
                    "conexao_solidaria_doacoes_processadas_total")
    item_prometheus(hostid, master, "doacoes.processadas.taxa", "Doacoes processadas por segundo",
                    "conexao_solidaria_doacoes_processadas_total", unidade="dps", taxa=True)
    item_prometheus(hostid, master, "doacoes.duplicadas", "Reentregas ignoradas (idempotencia)",
                    "conexao_solidaria_doacoes_duplicadas_total")
    item_prometheus(hostid, master, "doacoes.falhas", "Falhas de processamento (DLQ)",
                    "conexao_solidaria_doacoes_falhas_total")
    item_prometheus(hostid, master, "valor.arrecadado", "Valor consolidado",
                    "conexao_solidaria_valor_arrecadado_total")
    item_prometheus(hostid, master, "processo.memoria", "Memoria do processo",
                    "process_working_set_bytes", unidade="B")
    item_prometheus(hostid, master, "processo.cpu", "CPU do processo",
                    "process_cpu_seconds_total", unidade="%", taxa=True)

    garantir_trigger(
        "Conexao Solidaria: Worker fora do ar",
        "last(/Conexao Solidaria Worker/health.ready)=0 or nodata(/Conexao Solidaria Worker/health.ready,3m)=1",
        5,
        "O consumidor de doacoes parou; as mensagens ficarao acumuladas na fila.")

    garantir_trigger(
        "Conexao Solidaria: doacoes caindo na DLQ",
        "last(/Conexao Solidaria Worker/doacoes.falhas)>0",
        4,
        "Ha mensagens que falharam no processamento e foram para a dead letter queue.")

    # Cruza os dois hosts: o que a API aceitou ainda nao foi consolidado pelo Worker.
    garantir_trigger(
        "Conexao Solidaria: backlog de doacoes nao consolidadas",
        "last(/Conexao Solidaria API/doacoes.recebidas)-last(/Conexao Solidaria Worker/doacoes.processadas)>50",
        3,
        "O Worker nao esta drenando a fila no ritmo em que a API recebe doacoes.")

    return hostid


def provisionar_gateway(grupoid):
    hostid = garantir_host("Conexao Solidaria Gateway", grupoid, {"servico": "gateway", "camada": "borda"})
    garantir_item(hostid, "health.ready", {
        "name": "Health check (/health/live)",
        "type": ITEM_HTTP_AGENT,
        "value_type": VALOR_INTEIRO,
        "url": "http://gateway:8080/health/live",
        "delay": "30s",
        "timeout": "5s",
        "status_codes": "",
        "preprocessing": [
            {"type": PRE_JAVASCRIPT, "params": "return value.indexOf('Healthy') >= 0 ? 1 : 0;",
             "error_handler": 0, "error_handler_params": ""},
        ],
    })
    master = item_metricas(hostid, "http://gateway:8080/metrics")
    item_prometheus(hostid, master, "processo.memoria", "Memoria do processo",
                    "process_working_set_bytes", unidade="B")

    garantir_trigger(
        "Conexao Solidaria: Gateway fora do ar",
        "last(/Conexao Solidaria Gateway/health.ready)=0 or nodata(/Conexao Solidaria Gateway/health.ready,3m)=1",
        4,
        "O API Gateway parou de responder.")

    return hostid


def corrigir_host_do_agente():
    hosts = chamar("host.get", {"filter": {"host": ["Zabbix server"]}, "selectInterfaces": ["interfaceid", "dns", "useip"]})
    if not hosts:
        return

    for interface in hosts[0].get("interfaces", []):
        if interface.get("dns") == "zabbix-agent" and interface.get("useip") == "0":
            return
        chamar("hostinterface.update", {
            "interfaceid": interface["interfaceid"],
            "dns": "zabbix-agent",
            "useip": 0,
        })

    print("[zabbix-init] host 'Zabbix server' apontado para o container zabbix-agent")


def main():
    esperar_api()
    autenticar()

    corrigir_host_do_agente()
    grupoid = garantir_grupo()
    provisionar_api(grupoid)
    provisionar_worker(grupoid)
    provisionar_gateway(grupoid)

    hosts = chamar("host.get", {"groupids": grupoid, "selectItems": ["itemid"], "selectTriggers": ["triggerid"]})
    for h in hosts:
        print(f"[zabbix-init] {h['name']}: {len(h['items'])} itens, {len(h['triggers'])} triggers")

    print("[zabbix-init] provisionamento concluido")


if __name__ == "__main__":
    try:
        main()
    except Exception as erro:
        print(f"[zabbix-init] ERRO: {erro}", file=sys.stderr)
        sys.exit(1)
