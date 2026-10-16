# FindIt Backend

API ASP.NET Core 10 para o sistema de achados e perdidos FindIt da UEMG Divinópolis.

## Estado atual

- Entidades e mapeamento EF Core para usuários, categorias, etiquetas QR, itens, solicitações e histórico.
- Migrations em `FindIt.Api/Data/Migrations` e SQL gerado para conferência em `database/initial.sql`.
- Consultas públicas de itens e categorias.
- Fluxos autenticados de solicitação, cancelamento, aprovação, recusa e entrega.
- Ajuste excepcional do estado de um item, com motivo e histórico.
- Gestão administrativa de itens, etiquetas, categorias e permissões de administrador.
- Expiração automática de aprovações vencidas, ao iniciar a API e a cada minuto.
- Testes de regras de negócio com banco SQLite em memória.

A primeira migration foi aplicada ao MySQL local em 27/09/2026. Em 15/10/2026, a migration `RemoveRedundantQRCodeCode` retirou da tabela `qr_code` a coluna `Code`, que apenas repetia o GUID sem hífens; os endpoints de etiquetas retornam `Id` e `Guid`. Os registros FindIt API e FindIt Web foram criados no diretório Entra pessoal do desenvolvedor. O perfil principal do backend continua restrito ao tenant da UEMG, ao escopo `access_as_user` e ao cliente FindIt Web. A UEMG informou em 04/10/2026 que não autoriza aplicativos experimentais; portanto, esse perfil não pode ser validado com usuários institucionais neste TCC. O perfil de desenvolvimento aceita contas Microsoft pessoais e organizacionais para testes. Em 04/10/2026, o login com conta Microsoft pessoal e a rota `GET /api/me` foram confirmados no perfil de teste; contas organizacionais ainda não foram validadas. As consultas públicas funcionam sem token; as rotas autenticadas exigem um token válido.

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

O arquivo de segredos fica fora da pasta do projeto. Depois, na raiz do projeto, aplique as migrations pendentes:

```powershell
.\.tools\dotnet-ef database update --project FindIt.Api\FindIt.Api.csproj --startup-project FindIt.Api\FindIt.Api.csproj
```

O arquivo `database/initial.sql` contém o SQL gerado para todas as migrations até a versão atual. Use **as migrations ou o SQL**, sem executar ambos no mesmo banco.

## Login institucional

O registro **FindIt API** tem ID de aplicativo `a66cb6cf-367c-4568-b267-8e9f475a99bb`, URI `api://a66cb6cf-367c-4568-b267-8e9f475a99bb` e escopo `access_as_user`. O registro **FindIt Web** tem ID de aplicativo `b260a802-31c8-4321-9f4e-4b41ab44581f` e permissão delegada para esse escopo. Ambos pertencem ao diretório pessoal `abdd61f9-f485-42eb-8bbd-07609f52d09a`, que **não é** o diretório da UEMG. Esses IDs não são segredos.

O `AzureAd:ClientId` da API, `AzureAd:TenantId` da UEMG e `FindItAuth:WebClientId` estão em `appsettings.json`. A URL de login institucional e o erro de consentimento exibidos em 28/09/2026 indicam o locatário UEMG `e298359b-9b54-45c0-9bb7-690913b34cf8`. O middleware Microsoft valida assinatura, emissor e público do token; a política de autorização exige esse `tid`, o escopo `access_as_user` e o cliente FindIt Web por `azp` (token v2) ou `appid` (token v1). A identificação local usa o par `tid` + `oid` e aceita endereços `@discente.uemg.br` ou `@docente.uemg.br`. A validação institucional real depende de uma decisão futura da UEMG; não envie tokens no chat ou em issues.

### Teste de login pelo Swagger

No registro **FindIt Web**, em **Autenticação**, adicione a plataforma **Aplicativo de página única (SPA)** com a URI de redirecionamento `https://localhost:64529/swagger/oauth2-redirect.html`. O Swagger da API usa o fluxo de código de autorização com PKCE. Para o primeiro consentimento no diretório da UEMG, solicita `api://a66cb6cf-367c-4568-b267-8e9f475a99bb/.default` com `prompt=consent`; a permissão delegada configurada no FindIt Web continua sendo `access_as_user`, e o backend exige esse valor no token. Não crie um segredo de cliente para esse teste.

O perfil `FindIt.Api` do Visual Studio preserva essa configuração institucional. Em 04/10/2026, a UEMG negou a autorização de aplicativos experimentais, então o login institucional está bloqueado. Para testar as funcionalidades, use o perfil de teste descrito abaixo.

Em 28/09/2026, a primeira tentativa retornou `AADSTS650052`: o diretório da UEMG ainda não tinha uma entidade de serviço da FindIt API. Para habilitar o consentimento conjunto dos dois registros, abra **FindIt API → Manifesto**, localize `api.knownClientApplications` e adicione o ID do FindIt Web:

```json
"knownClientApplications": ["b260a802-31c8-4321-9f4e-4b41ab44581f"]
```

Essa configuração foi usada na tentativa de consentimento conjunto, para criar as entidades de serviço da API e do cliente. A permissão `access_as_user` fica em **FindIt Web → Permissões de APIs**; a entrada equivalente em **FindIt API → Permissões de APIs**, que solicitava o escopo da própria API, foi identificada como desnecessária. O teste do Swagger foi ajustado para o fluxo inicial de consentimento após o mesmo erro persistir.

Depois dessas correções, o fluxo chegou à tela **Necessidade de aprovação de administrador** para FindIt Web (editor não verificado). A UEMG respondeu que não concede acesso a aplicativos experimentais; a conta administradora do diretório pessoal não tem autoridade no diretório UEMG. O código do fluxo institucional permanece, mas não há teste de ponta a ponta com contas da universidade.

### Teste com contas Microsoft pessoais e organizacionais

Esse perfil usa a autoridade Microsoft `/common`, que permite contas pessoais e de trabalho/estudo de outros tenants. A API continua exigindo um token de acesso válido para a FindIt API, o escopo `access_as_user` e o cliente FindIt Web. Cada tenant organizacional pode exigir consentimento administrativo próprio; o perfil de teste não contorna a negativa da UEMG. Para configurar outro ambiente, faça no portal Entra:

1. Em **FindIt API → Autenticação → Tipos de conta com suporte**, selecione **Qualquer locatário de ID do Entra + Contas Pessoais da Microsoft**. Se o portal solicitar a versão do token, em **Manifesto → `api.requestedAccessTokenVersion`** defina `2` e salve. A API que aceita contas pessoais deve emitir tokens de acesso v2.
2. Faça a mesma mudança de **Tipos de conta com suporte** em **FindIt Web**. Mantenha a URI de redirecionamento SPA `https://localhost:64529/swagger/oauth2-redirect.html` e a permissão delegada `FindIt API / access_as_user` no FindIt Web.
3. No Visual Studio, pare a execução anterior (**Shift+F5**), selecione na lista ao lado do botão de execução o perfil **FindIt.Api (contas Microsoft - teste)** e inicie a API novamente. Os dois perfis usam a mesma porta; uma instância institucional ainda aberta continuará enviando o login para a UEMG. Em `https://localhost:64529/swagger`, clique em **Authorize** e entre com uma conta Microsoft. Depois execute `GET /api/me`. Para conferir o perfil ativo, `http://localhost:64530/swagger/v1/swagger.json` deve conter `/common/oauth2/v2.0/authorize` na URL de autorização.

Esse perfil existe somente para `Development`; a API rejeita a configuração de teste fora desse ambiente. O perfil comum **FindIt.Api** continua exigindo o locatário e o e-mail da UEMG. Se o token de alguma conta não trouxer e-mail ou UPN, `GET /api/me` pode retornar `403`; nesse caso, confira as claims localmente e configure no registro **FindIt API → Configuração do token** a claim opcional de e-mail para **token de acesso**. Não compartilhe o token completo.

**Resultados observados:** em 28/09/2026, a conta Outlook pessoal foi autorizada no Swagger e `GET /api/me` retornou o usuário local de ID 1, com `isAdmin: false`, no perfil anterior restrito a contas pessoais. Em 04/10/2026, o login com conta Microsoft pessoal e `GET /api/me` também foram confirmados pelo usuário no perfil de teste com `/common`. O acesso com conta organizacional ainda precisa de teste real.

Para testar as rotas administrativas no banco local, atribua uma vez `IsAdmin = 1` ao usuário de teste já criado. Confirme antes o `Id`, o `TenantId` e o e-mail na tabela `findit.user`; depois atualize somente essa linha. A API não promove automaticamente o primeiro usuário. Execute `GET /api/me` novamente e confira `isAdmin: true`. Com outra conta Microsoft, teste o papel de usuário comum: a nova conta deve receber `isAdmin: false`, poder abrir/cancelar solicitações próprias e receber `403` nas rotas `/api/admin/...`.

Se o login institucional vier a ser autorizado no futuro, um administrador inicial dessa população precisará ser atribuído uma única vez no banco. A API não promove automaticamente nenhum usuário.

### Integração com o frontend

Este repositório contém somente a API; o login validado até agora usa o Swagger. Para um frontend SPA em outra origem funcionar no navegador:

1. Cadastre a URL exata de retorno do frontend em **FindIt Web → Autenticação → Aplicativo de página única (SPA)**. A URI do Swagger não substitui a do frontend.
2. Configure a mesma origem (esquema, host e porta, sem barra final) em `Cors:AllowedOrigins` na API. O valor padrão é uma lista vazia; somente as origens explicitamente configuradas recebem cabeçalhos CORS. Exemplo de `appsettings.Development.json`, arquivo ignorado pelo Git:

   ```json
   { "Cors": { "AllowedOrigins": ["http://localhost:5173"] } }
   ```

3. No frontend, use o ID do **FindIt Web**, a autoridade `https://login.microsoftonline.com/common` para testes e o fluxo de código de autorização com PKCE. Solicite um **token de acesso** para `api://a66cb6cf-367c-4568-b267-8e9f475a99bb/access_as_user` e envie `Authorization: Bearer <token>` às rotas protegidas da API. O token de ID não serve para chamar a API. Não coloque segredo de cliente no navegador.
4. Aponte as requisições locais para `https://localhost:64529/api/...`. Teste primeiro `GET /api/me`, depois os fluxos de usuário e de administrador; as rotas administrativas dependem de `IsAdmin = 1` no banco.

O perfil **FindIt.Api (contas Microsoft - teste)** aceita tokens de qualquer tenant Microsoft apenas em `Development`. O frontend hospedado em outro ambiente precisará de uma configuração de testes própria; não publique esse perfil como configuração de produção.

## Execução e testes

```powershell
dotnet build FindIt.slnx
dotnet run --project FindIt.Api\FindIt.Api.csproj
dotnet test FindIt.Api.Tests\FindIt.Api.Tests.csproj
```

O Swagger UI aparece no ambiente de desenvolvimento. As rotas públicas são `GET /api/items`, `GET /api/items/{id}`, `GET /api/items/by-qr/{guid}`, `GET /api/items/recently-closed`, `GET /api/items/recently-returned` e `GET /api/categories`. A rota `recently-returned` lista apenas itens devolvidos nos últimos sete dias; `recently-closed` também inclui arquivados. As demais rotas exigem token; as rotas `/api/admin/...` também conferem `isAdmin` no banco.

Para editar apenas alguns dados de um item, use `PATCH /api/admin/items/{id}` e envie somente os campos desejados, por exemplo `{ "name": "Novo nome" }`. Os demais dados permanecem como estão. O endpoint `PUT /api/admin/items/{id}` continua exigindo todos os campos editáveis.

### Fotos dos itens

Todo novo item precisa de uma etiqueta QR previamente gerada e ainda não vinculada. Informe `qrCodeGuid` em `POST /api/admin/items`; o backend rejeita o cadastro sem etiqueta. Itens antigos sem etiqueta permanecem no banco.

Para reimpressão, `GET /api/admin/items/{id}/qr-code` retorna a etiqueta vinculada ao item para um administrador. Retorna 404 quando o item não tem etiqueta.

O cadastro continua em duas etapas: `POST /api/admin/items` recebe JSON e retorna `{ "id": 42 }`; depois `PUT /api/admin/items/42/photos` recebe `multipart/form-data`, com até três partes chamadas `files`. A rota substitui todo o conjunto de fotos e também funciona na edição. Enviar um formulário multipart vazio remove todas as fotos. Apenas administradores podem alterar fotos. Os DTOs públicos de itens incluem `photos: [{ "id": 1 }]`, e cada imagem pode ser lida em `GET /api/items/{id}/photos/{photoId}`.

Execute a migration `AddItemPhotos` antes de usar as novas rotas. Os arquivos ficam, por padrão, em `%LOCALAPPDATA%\FindIt\photos` no Windows, fora do repositório. Para o desenvolvimento local, configure `Photos:RootPath` em `FindIt.Api/appsettings.Development.json`; nesta máquina ele aponta para `C:\Users\Rafae\Desktop\FindIt-data\photos`, ao lado das pastas do backend e do frontend. A API cria a pasta no primeiro envio de fotos. Inclua essa pasta no backup junto com o MySQL. `Photos:MaxCount` e `Photos:MaxBytes` controlam os limites (padrões: 3 arquivos e 5 MB por arquivo). São aceitos JPEG, PNG e WebP. O navegador não recebe acesso direto à pasta. Uma futura melhoria é substituir o armazenamento em disco por um serviço de objetos na nuvem.

As datas persistidas são UTC. A aprovação vence à meia-noite **após** o quinto dia útil, no horário de Brasília; assim, continua válida durante toda a faixa das 23h59 desse dia. Sábados e domingos não contam; feriados não são considerados nesta versão.
