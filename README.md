# FindIt Backend

API ASP.NET Core 10 do protótipo FindIt, sistema de achados e perdidos para ambiente universitário. O [frontend](https://github.com/rafabalves/findIt-frontend) está em outro repositório.

## Funcionalidades

- Consulta pública de itens e categorias.
- Autenticação Microsoft e autorização de usuários e administradores.
- Cadastro de itens, fotografias, categorias e etiquetas QR.
- Solicitações de recuperação, aprovação, código de retirada e registro da devolução.
- Histórico das operações e testes das principais regras de negócio.

## Requisitos

- .NET SDK 10
- MySQL Server 8.4
- Registros FindIt API e FindIt Web no Microsoft Entra ID para testar o login

## Configuração local

Crie um banco MySQL e um usuário com acesso a ele. Configure a conexão em `FindIt.Api/appsettings.Development.json`, arquivo ignorado pelo Git:

```json
{
  "ConnectionStrings": {
    "FindIt": "server=localhost;port=3306;database=findit;user=SEU_USUARIO;password=SUA_SENHA;SslMode=Disabled;AllowPublicKeyRetrieval=True"
  },
  "Photos": {
    "RootPath": "CAMINHO_ABSOLUTO_PARA_AS_FOTOS"
  }
}
```

`Photos:RootPath` é opcional. Sem ele, as fotos são armazenadas em `%LOCALAPPDATA%\FindIt\photos` no Windows. Não publique credenciais ou fotografias reais no repositório.

Na raiz do projeto, restaure os pacotes, instale a ferramenta EF Core e aplique as migrations:

```powershell
dotnet restore FindIt.slnx --configfile NuGet.Config
dotnet tool install dotnet-ef --tool-path .tools --version 10.0.12 --configfile NuGet.Config
.\.tools\dotnet-ef database update --project FindIt.Api\FindIt.Api.csproj --startup-project FindIt.Api\FindIt.Api.csproj
```

O arquivo `database/initial.sql` contém o esquema gerado pelas migrations. Use **as migrations ou esse SQL**, sem executar ambos no mesmo banco.

## Executar e testar

Para testar com contas Microsoft pessoais:

```powershell
dotnet run --project FindIt.Api\FindIt.Api.csproj --launch-profile 'FindIt.Api (contas Microsoft - teste)'
```

O Swagger fica em `https://localhost:64529/swagger`. Após entrar, execute `GET /api/me` para criar ou recuperar o usuário local. As rotas públicas dispensam login; as rotas `/api/admin/...` exigem `IsAdmin = true` no banco. Nenhum usuário recebe essa permissão automaticamente.

```powershell
dotnet build FindIt.slnx
dotnet test FindIt.Api.Tests\FindIt.Api.Tests.csproj
```

O frontend local usa `http://localhost:5173` e encaminha `/api` para esta API. Registre essa URL como URI de redirecionamento SPA no aplicativo **FindIt Web** do Entra ID. Para o login pelo Swagger, registre também `https://localhost:64529/swagger/oauth2-redirect.html`.

## Autenticação e limites da demonstração

O perfil `FindIt.Api` mantém a configuração planejada para contas institucionais da UEMG. Esse fluxo não foi validado de ponta a ponta porque a universidade não autorizou o consentimento do aplicativo experimental. Os testes usam o perfil **contas Microsoft - teste**, com autoridade `/common`. A API continua validando token de acesso, escopo `access_as_user` e aplicativo cliente FindIt Web.

O perfil `FindIt.Api (demonstração)` executa no ambiente `Demo`, escuta em `http://127.0.0.1:64530` e não expõe o Swagger. Na demonstração temporária, a API, o MySQL e as fotografias permanecem no computador do desenvolvedor; o acesso externo à API depende de um túnel HTTPS. Essa configuração não representa implantação institucional ou permanente.

As fotografias são armazenadas em disco. Por padrão, cada item aceita até três arquivos JPEG, PNG ou WebP de até 5 MB cada. O armazenamento em serviço de objetos é uma melhoria futura.
