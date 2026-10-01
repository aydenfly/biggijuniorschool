using BJS.Master.Models;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.Configure<SmtpModel>(builder.Configuration.GetSection("SMTP"));
builder.Services.Configure<MailjetModel>(builder.Configuration.GetSection("Mailjet"));

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
    options.AppendTrailingSlash = false;
});

var app = builder.Build();

var options = new RewriteOptions()
            .AddRewrite(@"^about", "/", skipRemainingRules: true)
            .AddRewrite(@"^leadership", "/", skipRemainingRules: true)
            .AddRewrite(@"^history", "/", skipRemainingRules: true)
            .AddRewrite(@"^gallery", "/", skipRemainingRules: true)
            .AddRewrite(@"^contact", "/", skipRemainingRules: true)
            .AddRewrite(@"^activities", "/", skipRemainingRules: true)
            .AddRewrite(@"^tuition", "/", skipRemainingRules: true)
            .AddRewrite(@"^privacy", "/", skipRemainingRules: true);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/errors/{0}");
app.UseRewriter(options);
app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = SetHttpHeaders()
});
app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.Strict,
});

app.UseRouting();

app.MapRazorPages();

// SPA routes
// Any path that isn't a static file or an explicit page (e.g. /about,
// /leadership, /privacy) falls back to Index, which contains your SPA.
// The page's own JS then reads the URL and scrolls to the right section.
app.MapFallbackToPage("/Index");

app.Run();

static Action<StaticFileResponseContext> SetHttpHeaders()
{
    return options =>
    {
        var headers = options.Context.Response.GetTypedHeaders();
        headers.CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue
        {
            Public = true,
            MaxAge = TimeSpan.FromDays(365)
        };
        headers.Expires = new DateTimeOffset(DateTime.UtcNow.AddDays(365));
    };
}
