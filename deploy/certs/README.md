# Certificados de CA adicionais (opcional)

Redes corporativas e antivírus com "SSL scanning" (Avast, Kaspersky, Zscaler, proxies MITM)
reassinam o tráfego HTTPS com uma CA própria. O container não conhece essa CA e o
`dotnet restore` falha com `NU1301: The SSL connection could not be established`.

Se o build da imagem falhar assim, exporte a CA em formato PEM e salve **aqui** com
extensão `.crt`. Os Dockerfiles instalam automaticamente tudo que estiver nesta pasta
antes do restore. Em uma rede sem interceptação a pasta fica vazia e nada muda.

Exportando no Windows (PowerShell), procurando pela CA que assinou o tráfego:

```powershell
$store = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root","LocalMachine")
$store.Open("ReadOnly")
$c = $store.Certificates | Where-Object { $_.Subject -like "*Avast*" } | Select-Object -First 1
$b64 = [Convert]::ToBase64String($c.RawData, 'InsertLineBreaks')
Set-Content deploy\certs\corp-ca.crt "-----BEGIN CERTIFICATE-----`n$b64`n-----END CERTIFICATE-----" -Encoding ascii
$store.Close()
```

Exportando no Linux/macOS:

```bash
openssl s_client -showcerts -connect api.nuget.org:443 </dev/null 2>/dev/null \
  | openssl x509 -outform PEM > deploy/certs/corp-ca.crt
```

Arquivos `.crt` desta pasta são ignorados pelo Git — são específicos da sua máquina.
Este README existe para que a pasta sobreviva ao versionamento: o Git não guarda
diretórios vazios.
