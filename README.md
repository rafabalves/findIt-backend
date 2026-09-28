# FindIt Backend

API ASP.NET Core 10 para o sistema de achados e perdidos FindIt da UEMG Divinópolis.

## Estado atual

- Entidades e mapeamento EF Core para usuários, categorias, etiquetas QR, itens, solicitações e histórico.
- Primeira migration em `FindIt.Api/Data/Migrations` e SQL gerado para conferência em `database/initial.sql`.
- Consultas públicas de itens e categorias.
- Fluxos autenticados de solicitação, cancelamento, aprovação, recusa e entrega.
- Ajuste excepcional do estado de um item, com motivo e histórico.
- Gestão administrativa de itens, etiquetas, categorias e permissões de administrador.
- Expiração automática de aprovações vencidas, ao iniciar a API e a cada minuto.
- Testes de regras de negócio com banco SQLite em memória.

A primeira migration foi aplicada ao MySQL local em 27/09/2026. Os registros FindIt API e FindIt Web foram criados no diretório Entra pessoal do desenvolvedor. O perfil principal do backend valida tokens da UEMG destinados à API, com o escopo `access_as_user` e o FindIt Web como cliente. O perfil de desenvolvimento para contas pessoais foi validado em 28/09/2026: o Swagger obteve autorização e `GET /api/me` retornou HTTP 200, criando o usuário local. O teste institucional ainda depende de consentimento da UEMG. As consultas públicas funcionam sem token; as rotas autenticadas exigem um token válido.

## Requisitos

- .NET SDK 10
- MySQL Server 8.4
- Ferramenta `dotnet-ef` 10.0.12 para aplicar migrations

Na raiz do projeto, instale a ferramenta local e restaure os pacotes:

```powershell
dotnet tool install dotnet-ef --tool-path .tools --version 10.0.12 --configfile NuGet.Config
dotnet restore FindIt.Api.Tests\FindIt.Api.Tests.csproj --configfile NuGet.Config
```

## Banco local

No MySQL Workbench, conectado como `root`, execute o SQL abaixo. Substitua a senha de exemplo por uma senha nova e **não a envie no chat nem a adicione ao Git**.

```sql
CREATE DATABASE findit CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER 'findit_app'@'localhost' IDENTIFIED BY 'SUBSTITUA_POR_SENHA_FORTE';
GRANT ALL PRIVILEGES ON findit.* TO 'findit_app'@'localhost';
```

Em Visual Studio, clique com o botão direito em `FindIt.Api` e abra **Gerenciar Segredos do Usuário**. Preencha o arquivo local de segredos:

```json
{
  "ConnectionStrings": {
    "FindIt": "server=localhost;port=3306;database=findit;user=findit_app;password=SUA_SENHA;SslMode=Disabled;AllowPublicKeyRetrieval=True"
  }
}
```

O arquivo de segredos fica fora da pasta do projeto. Depois, na raiz do projeto, aplique a migration:

```powershell
.\.tools\dotnet-ef database update --project FindIt.Api\FindIt.Api.csproj --startup-project FindIt.Api\FindIt.Api.csproj
```

O arquivo `database/initial.sql` permite inspecionar a estrutura gerada. Use **a migration ou o SQL**, sem executar ambos no mesmo banco.

## Login institucional

O registro **FindIt API** tem ID de aplicativo `a66cb6cf-367c-4568-b267-8e9f475a99bb`, URI `api://a66cb6cf-367c-4568-b267-8e9f475a99bb` e escopo `access_as_user`. O registro **FindIt Web** tem ID de aplicativo `b260a802-31c8-4321-9f4e-4b41ab44581f` e permissão delegada para esse escopo. Ambos pertencem ao diretório pessoal `abdd61f9-f485-42eb-8bbd-07609f52d09a`, que **não é** o diretório da UEMG. Esses IDs não são segredos.

O `AzureAd:ClientId` da API, `AzureAd:TenantId` da UEMG e `FindItAuth:WebClientId` estão em `appsettings.json`. A URL de login institucional e o erro de consentimento exibidos em 28/09/2026 indicam o locatário UEMG `e298359b-9b54-45c0-9bb7-690913b34cf8`. O middleware Microsoft valida assinatura, emissor e público do token; a política de autorização exige esse `tid`, o escopo `access_as_user` e o cliente FindIt Web por `azp` (token v2) ou `appid` (token v1). A identificação local usa o par `tid` + `oid` e aceita endereços `@discente.uemg.br` ou `@docente.uemg.br`. O fluxo institucional ainda precisa ser testado com um token real; não envie tokens no chat ou em issues.

### Teste de login pelo Swagger

No registro **FindIt Web**, em **Autenticação**, adicione a plataforma **Aplicativo de página única (SPA)** com a URI de redirecionamento `https://localhost:64529/swagger/oauth2-redirect.html`. O Swagger da API usa o fluxo de código de autorização com PKCE. Para o primeiro consentimento no diretório da UEMG, solicita `api://a66cb6cf-367c-4568-b267-8e9f475a99bb/.default` com `prompt=consent`; a permissão delegada configurada no FindIt Web continua sendo `access_as_user`, e o backend exige esse valor no token. Não crie um segredo de cliente para esse teste.

Depois de iniciar a API no perfil `FindIt.Api` do Visual Studio, abra `https://localhost:64529/swagger`, clique em **Authorize**, selecione o escopo e entre com a conta institucional. Em seguida, execute `GET /api/me`. A primeira autenticação ainda pode ser bloqueada pela política de consentimento da UEMG; nesse caso, será necessário envolver a TI. Confirme o `tid` apenas localmente no token recebido e não compartilhe o token completo.

Em 28/09/2026, a primeira tentativa retornou `AADSTS650052`: o diretório da UEMG ainda não tinha uma entidade de serviço da FindIt API. Para habilitar o consentimento conjunto dos dois registros, abra **FindIt API → Manifesto**, localize `api.knownClientApplications` e adicione o ID do FindIt Web:

```json
"knownClientApplications": ["b260a802-31c8-4321-9f4e-4b41ab44581f"]
```

Salve o manifesto e tente entrar novamente. Essa configuração permite que a Microsoft crie as entidades de serviço da API e do cliente no mesmo fluxo de consentimento. A permissão `access_as_user` deve estar em **FindIt Web → Permissões de APIs**; a entrada equivalente em **FindIt API → Permissões de APIs**, que solicitava o escopo da própria API, foi identificada como desnecessária. O teste do Swagger foi ajustado para o fluxo inicial de consentimento após o mesmo erro persistir.

Depois dessas correções, o fluxo chegou à tela **Necessidade de aprovação de administrador** para FindIt Web (editor não verificado). O bloqueio agora é a política de consentimento do diretório da UEMG: alguém com as permissões administrativas adequadas na UEMG precisa avaliar e, se aprovar, conceder consentimento aos registros FindIt Web e FindIt API. A conta administradora do diretório pessoal não tem autoridade no diretório UEMG. Até essa aprovação, o login institucional real e `GET /api/me` permanecem sem teste de ponta a ponta. O desenvolvimento local das demais funções pode continuar.

### Teste com conta Outlook pessoal

Esse teste verifica a autenticação, a emissão do token para a FindIt API e o acesso a `GET /api/me`. Não substitui a aprovação necessária para contas da UEMG. Para configurar outro ambiente, faça no portal Entra:

1. Em **FindIt API → Autenticação → Tipos de conta com suporte**, selecione **Qualquer locatário de ID do Entra + Contas Pessoais da Microsoft**. Se o portal solicitar a versão do token, em **Manifesto → `api.requestedAccessTokenVersion`** defina `2` e salve. A API que aceita contas pessoais deve emitir tokens de acesso v2.
2. Faça a mesma mudança de **Tipos de conta com suporte** em **FindIt Web**. Mantenha a URI de redirecionamento SPA `https://localhost:64529/swagger/oauth2-redirect.html` e a permissão delegada `FindIt API / access_as_user` no FindIt Web.
3. No Visual Studio, pare a execução anterior (**Shift+F5**), selecione na lista ao lado do botão de execução o perfil **FindIt.Api (Outlook pessoal - teste)** e inicie a API novamente. Os dois perfis usam a mesma porta; uma instância institucional ainda aberta continuará enviando o login para a UEMG. Em `https://localhost:64529/swagger`, clique em **Authorize** e entre com a conta Outlook pessoal. Depois execute `GET /api/me`. O perfil usa o locatário de contas pessoais da Microsoft; a conta precisa consentir com a permissão solicitada. Para conferir o perfil ativo, `http://localhost:64530/swagger/v1/swagger.json` deve conter `/9188040d-6c67-4c5b-b112-36a304b66dad/oauth2/v2.0/authorize` na URL de autorização.

Esse perfil existe somente para `Development`. O perfil comum **FindIt.Api** continua exigindo o locatário e o e-mail da UEMG. A política da API, em ambos os perfis, também exige o escopo `access_as_user` e o cliente FindIt Web. Se o token da conta pessoal não trouxer e-mail ou nome de usuário, `GET /api/me` pode retornar `403`; nesse caso, confira as claims localmente e configure no registro **FindIt API → Configuração do token** a claim opcional de e-mail para **token de acesso**. Não compartilhe o token completo.

**Resultado observado em 28/09/2026:** a conta Outlook pessoal foi autorizada no Swagger e `GET /api/me` retornou o usuário local de ID 1, com `isAdmin: false`. Isso confirma o fluxo completo no perfil pessoal. O primeiro acesso institucional permanece bloqueado pela política de consentimento da UEMG.

Para testar as rotas administrativas no banco local, atribua uma vez `IsAdmin = 1` ao usuário de teste já criado. Confirme antes o `Id`, o `TenantId` de contas pessoais e o e-mail na tabela `findit.user`; depois atualize somente essa linha. A API não promove automaticamente o primeiro usuário. Execute `GET /api/me` novamente e confira `isAdmin: true`. Com outra conta Outlook pessoal, teste o papel de usuário comum: a nova conta deve receber `isAdmin: false`, poder abrir/cancelar solicitações próprias e receber `403` nas rotas `/api/admin/...`.

Após o primeiro login institucional, um administrador inicial precisa ser atribuído uma única vez no banco. Essa etapa será feita quando o login real estiver funcional; a API não promove automaticamente nenhum usuário.

## Execução e testes

```powershell
dotnet build FindIt.slnx
dotnet run --project FindIt.Api\FindIt.Api.csproj
dotnet test FindIt.Api.Tests\FindIt.Api.Tests.csproj
```

O Swagger UI aparece no ambiente de desenvolvimento. As rotas públicas são `GET /api/items`, `GET /api/items/{id}`, `GET /api/items/by-qr/{guid}`, `GET /api/items/recently-closed` e `GET /api/categories`. As demais rotas exigem token; as rotas `/api/admin/...` também conferem `isAdmin` no banco.

As datas persistidas são UTC. A aprovação vence à meia-noite **após** o quinto dia útil, no horário de Brasília; assim, continua válida durante toda a faixa das 23h59 desse dia. Sábados e domingos não contam; feriados não são considerados nesta versão.
