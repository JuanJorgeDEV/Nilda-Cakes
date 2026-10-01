using System.Globalization;
using DeliciasDaNilda.Common;
using DeliciasDaNilda.Data;
using DeliciasDaNilda.Endpoints;
using DeliciasDaNilda.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

// Datas do sistema são gravadas em UTC "cru" (DateTime sem fuso): mantém o comportamento clássico do Npgsql.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Exibição sempre em pt-BR (R$, datas), mesmo num servidor em outro idioma.
var ptBr = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = ptBr;
CultureInfo.DefaultThreadCurrentUICulture = ptBr;

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    // RN02: todo o painel ADM exige autenticação, exceto a própria tela de
    // login (marcada com [AllowAnonymous] em Login.cshtml.cs).
    options.Conventions.AuthorizeFolder("/Admin");
})
.AddMvcOptions(options =>
{
    // Decimais aceitam "0.5" (input number) e "0,5" (pt-BR) — ver DecimalModelBinder.
    options.ModelBinderProviders.Insert(0, new DecimalModelBinderProvider());
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(PostgresConexao.Normalizar(builder.Configuration.GetConnectionString("DefaultConnection"))));

// Atrás do proxy do Render/Supabase: respeita X-Forwarded-Proto/For.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Services de domínio (RN03, RN05-RN15).
builder.Services.AddScoped<IPrecificacaoService, PrecificacaoService>();
builder.Services.AddScoped<IAgendamentoService, AgendamentoService>();
builder.Services.AddScoped<IEstoqueService, EstoqueService>();
builder.Services.AddScoped<IPedidoService, PedidoService>();
builder.Services.AddScoped<IProjecaoEstoqueService, ProjecaoEstoqueService>();
builder.Services.AddScoped<IPagamentoService, PagamentoService>();
builder.Services.AddScoped<IAgendaService, AgendaService>();
builder.Services.AddScoped<IAdministradorAuthService, AdministradorAuthService>();
builder.Services.AddScoped<ISaborService, SaborService>();
builder.Services.AddScoped<IIngredienteService, IngredienteService>();
builder.Services.AddScoped<IConfiguracaoService, ConfiguracaoService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IMensagemBotService, MensagemBotService>();
builder.Services.AddScoped<IFinanceiroService, FinanceiroService>();

// Bot do WhatsApp: o site sobe o processo Node junto e guarda o QR/estado da conexão.
builder.Services.AddSingleton<WhatsAppBotProcess>();
builder.Services.AddSingleton<IWhatsAppBotProcess>(sp => sp.GetRequiredService<WhatsAppBotProcess>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<WhatsAppBotProcess>());

// Sessão simples usada apenas para a identificação simplificada de Cliente
// (nome + WhatsApp, sem senha — RN01). O ADM (RN02) usa autenticação por
// cookie de verdade, configurada abaixo.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Autenticação do ADM via cookie (RN02).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Admin/Login";
        options.AccessDeniedPath = "/Admin/Login";
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();

app.UseRouting();

// Autenticação exclusiva dos endpoints internos do bot de WhatsApp
// (X-Internal-Api-Key), restrita ao prefixo /internal/whatsapp-bot — não
// afeta Razor Pages nem a autenticação por cookie do ADM abaixo.
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/internal/whatsapp-bot"),
    branch => branch.UseMiddleware<InternalApiKeyMiddleware>());

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Fotos de sabores são enviadas em tempo de execução (wwwroot/uploads): precisam do middleware clássico.
app.UseStaticFiles();
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapWhatsAppBotEndpoints();

// Verificação de saúde para o Render e para o "pinger" que mantém o serviço acordado.
app.MapGet("/saude", () => Results.Ok("ok")).AllowAnonymous();

// SEED DE DESENVOLVIMENTO/PROTÓTIPO ACADÊMICO (RN02):
// não existe cadastro público de Administrador (não deveria haver uma tela
// aberta de "criar administrador" sem controle nenhum), então criamos o
// primeiro ADM aqui, uma única vez, se a tabela estiver vazia, usando
// credenciais lidas de configuração (AdminSeed:Email / AdminSeed:Senha em
// appsettings.json ou appsettings.Development.json, ou variáveis de
// ambiente/user-secrets equivalentes). Uma gestão de administradores mais
// robusta (múltiplos admins, troca de senha pela própria aplicação, etc.)
// fica para uma fase futura — não implementada aqui.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Em hospedagem (sem terminal para rodar "dotnet ef"), aplica as migrations ao subir.
    if (app.Configuration.GetValue("Database:MigrarAoIniciar", false))
    {
        await db.Database.MigrateAsync();
    }

    if (!await db.Administradores.AnyAsync())
    {
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var seedEmail = config["AdminSeed:Email"];
        var seedSenha = config["AdminSeed:Senha"];

        if (!string.IsNullOrWhiteSpace(seedEmail) && !string.IsNullOrWhiteSpace(seedSenha))
        {
            var authService = scope.ServiceProvider.GetRequiredService<IAdministradorAuthService>();
            await authService.CriarAdministradorAsync("Administrador", seedEmail, seedSenha);
        }
    }

    // SEED DE CONFIGURAÇÃO GLOBAL (tabela singleton — não é RN numerada):
    // garante que sempre exista exatamente uma linha em Configuracoes,
    // usada, entre outras coisas, para ligar/desligar o bot de atendimento
    // automático via WhatsApp pelo painel ADM.
    if (!await db.Configuracoes.AnyAsync())
    {
        db.Configuracoes.Add(new DeliciasDaNilda.Domain.Entities.Configuracao
        {
            WhatsAppBotAtivo = false,
            AtualizadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}

// DEMONSTRAÇÃO: com Demo:Seed=true, popula um banco VAZIO (4 bolos, estoque e pedidos fictícios) ao subir.
if (app.Configuration.GetValue("Demo:Seed", false))
{
    var demo = await DeliciasDaNilda.Data.DevSeed.ExecutarAsync(app.Services, app.Environment, reset: false, permitirForaDeDev: true);
    Console.WriteLine(demo.Executado ? "Seed de demonstração aplicado." : "Seed de demonstração ignorado: " + demo.MotivoAborto);
}

// SEED DE DADOS DE TESTE (só sob demanda): `dotnet run -- --seed-dev [--reset]`.
// Nunca roda automaticamente; recusa fora de Development; encerra sem subir o
// servidor web. Ver Data/DevSeed.cs e docs/como-rodar.md.
if (args.Contains("--seed-dev"))
{
    try
    {
        var seed = await DeliciasDaNilda.Data.DevSeed.ExecutarAsync(
            app.Services, app.Environment, reset: args.Contains("--reset"));

        if (!seed.Executado)
        {
            Console.Error.WriteLine("SEED ABORTADO: " + seed.MotivoAborto);
            Environment.ExitCode = 1;
        }
        else
        {
            Console.WriteLine("=== Seed de desenvolvimento concluído ===");
            Console.WriteLine($"Sabores: {seed.Sabores} | Ingredientes: {seed.Ingredientes} | Clientes: {seed.Clientes} | Pedidos: {seed.Pedidos}");
            foreach (var c in seed.Cenarios) Console.WriteLine(" - " + c);
            Console.WriteLine();
            foreach (var a in seed.Avisos) Console.WriteLine("ATENÇÃO: " + a);
            Console.WriteLine();
            Console.WriteLine("Admin: " + app.Configuration["AdminSeed:Email"] + " (senha em AdminSeed:Senha)");
            Console.WriteLine("URLs (após `dotnet run`): /Admin/Login, /Admin/Pedidos/Index, "
                + $"/Admin/Agenda/Index?data={seed.DiaLotado:yyyy-MM-dd}, /Admin/Estoque/Alertas, /Cliente/Identificacao");
            Console.WriteLine("Clientes de teste (WhatsApp): 5516999990001 a 5516999990006");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("SEED FALHOU: " + ex.Message);
        Environment.ExitCode = 1;
    }
    return;
}

app.Run();
