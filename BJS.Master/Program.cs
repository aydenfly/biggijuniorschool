using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

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
app.UseStaticFiles();
app.UseRouting();

app.MapRazorPages();

// SPA routes
// Any path that isn't a static file or an explicit page (e.g. /about,
// /leadership, /privacy) falls back to Index, which contains your SPA.
// The page's own JS then reads the URL and scrolls to the right section.
app.MapFallbackToPage("/Index");

app.Run();