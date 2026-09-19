# Delícias da Nilda

Sistema de encomendas para uma confeitaria artesanal. O cliente escolhe o sabor, o peso e a data de entrega; a confeiteira acompanha os pedidos, a agenda, o estoque de ingredientes e os pagamentos em um painel próprio.

Projeto de extensão universitária (UniSENAI, Ribeirão Preto), feito com uma confeitaria de verdade.

## O que ele faz

- **Pedidos pelo site:** o cliente se identifica com nome e WhatsApp, escolhe o sabor e o peso e vê o valor na hora. O preço é sempre peso × preço por quilo do sabor.
- **Agenda com limite:** há um teto de bolos por dia e uma antecedência mínima para encomendar. Datas lotadas ficam bloqueadas no calendário.
- **Painel da confeiteira:** confirmar ou cancelar pedidos, ver a agenda do dia, cadastrar sabores com a ficha técnica, controlar entradas, perdas e saldo de ingredientes e registrar pagamentos.
- **Estoque com aviso:** o sistema abate os ingredientes quando um pedido é confirmado e avisa quando o estoque não cobre os próximos pedidos.
- **Atendimento pelo WhatsApp (opcional):** um bot pode receber pedidos pelo WhatsApp. A confeiteira liga e desliga pelo painel. Detalhes em [`whatsapp-bot/`](whatsapp-bot/README.md).

## Tecnologias

- .NET 10 (C#), ASP.NET Core Razor Pages
- Entity Framework Core com SQL Server
- Bootstrap 5
- xUnit para os testes
- Node.js com Baileys para o bot de WhatsApp

## Como rodar

Você precisa do [SDK do .NET 10](https://dotnet.microsoft.com/download) e de um SQL Server. Em Windows, o LocalDB que vem com o Visual Studio já serve, e a conexão padrão em `appsettings.json` aponta para ele.

```bash
# 1. Configuração local (o arquivo copiado não vai para o Git)
cp src/DeliciasDaNilda/appsettings.Development.example.json src/DeliciasDaNilda/appsettings.Development.json
# edite o novo arquivo: defina e-mail e senha do administrador e uma chave para a API do bot

# 2. Banco de dados
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/DeliciasDaNilda

# 3. Subir o site
dotnet run --project src/DeliciasDaNilda
```

O site abre em `http://localhost:5138`. Na primeira execução, o administrador é criado com o e-mail e a senha que você definiu em `AdminSeed`.

### Dados de exemplo

Para testar com pedidos, sabores e estoque de mentira (só funciona em ambiente de desenvolvimento):

```bash
dotnet run --project src/DeliciasDaNilda -- --seed-dev
```

Use `--seed-dev --reset` para apagar os dados de teste e recriar. As fichas técnicas geradas são fictícias.

### Testes

```bash
dotnet test tests/DeliciasDaNilda.Tests
```

## Organização das pastas

```
src/DeliciasDaNilda/   aplicação web (páginas, serviços, acesso a dados)
tests/                 testes automatizados
whatsapp-bot/          bot de WhatsApp (Node.js)
```

## Segurança

Senhas, chaves e a sessão do WhatsApp ficam em arquivos locais que estão no `.gitignore`. Em produção, use variáveis de ambiente (por exemplo `ConnectionStrings__DefaultConnection` e `AdminSeed__Senha`) em vez de arquivos.
