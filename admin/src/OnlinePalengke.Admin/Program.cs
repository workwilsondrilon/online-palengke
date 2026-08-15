using OnlinePalengke.Admin.Api;
using OnlinePalengke.Admin.Auth;
using OnlinePalengke.Admin.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// AddAuthorizationCore is for AuthorizeRouteView/IAuthorizationService, evaluated purely
// inside the Blazor circuit against AdminAuthenticationStateProvider's JWT session - see
// the .AllowAnonymous() on MapRazorComponents below for why the HTTP layer itself must
// stay out of this decision entirely.
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

// Registered as both the concrete type (so components/AdminApiClient can call its
// session-management methods) and the AuthenticationStateProvider it implements.
builder.Services.AddScoped<AdminAuthenticationStateProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(
    sp => sp.GetRequiredService<AdminAuthenticationStateProvider>());

builder.Services.AddHttpClient<AdminApiClient>(client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("The 'Api:BaseUrl' configuration value is missing.");
    client.BaseAddress = new Uri(baseUrl);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    // Razor Components routing projects each page's own [Authorize] attribute onto that
    // route's HTTP endpoint metadata, which would otherwise make ASP.NET Core's
    // authorization middleware enforce it (and crash - no auth scheme exists to
    // Challenge() with) before the Blazor circuit ever connects. Worse, even with a
    // scheme wired up purely to redirect, that HTTP-level check would fire on every
    // full-page load - including a reload of an already-signed-in tab - and bounce the
    // admin straight back to /login before the circuit gets a chance to reconnect and
    // read the persisted session out of ProtectedSessionStorage, defeating the entire
    // point of persisting it. AllowAnonymous here overrides the per-page [Authorize] for
    // HTTP purposes only (AllowAnonymous metadata always wins when both are present on
    // the same endpoint); AuthorizeRouteView (Routes.razor) still enforces those same
    // [Authorize] attributes for real, but only once inside the circuit, after
    // AdminAuthenticationStateProvider has had the chance to hydrate from storage.
    .AllowAnonymous();

app.Run();
