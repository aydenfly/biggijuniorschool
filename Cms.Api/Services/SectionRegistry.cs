using Bjs.Cms.Contracts;

namespace Bjs.Cms.Api.Services;

/// <summary>Every editable part of the site, how it is grouped in the dashboard, and the model that describes it.</summary>
public static class SectionRegistry
{
    public record Def(string Key, string Title, string Group, string Description, Type Type, string? Route, bool HasSeo);

    public static readonly IReadOnlyList<Def> All =
    [
        new("hero", "Homepage hero", "Pages", "Slideshow images and the text laid over them.", typeof(HeroSection), "/", true),
        new("about", "About us", "Pages", "Image card, introduction and key facts.", typeof(AboutSection), "/about", true),
        new("leadership", "Leadership", "Pages", "The people shown on the leadership cards.", typeof(LeadershipSection), "/leadership", true),
        new("history", "History", "Pages", "The school's story.", typeof(TextSection), "/history", true),
        new("socialwall", "Social wall", "Pages", "Photo collections grouped by type.", typeof(GallerySection), "/gallery", true),
        new("activities", "Activities & facilities", "Pages", "Clubs, sports and facilities with photo collections.", typeof(GallerySection), "/activities", true),
        new("contact", "Get in touch", "Pages", "Map, address and contact details.", typeof(ContactSection), "/contact", true),
        new("tuition", "Tuition & fees", "Pages", "Fees information and the support button.", typeof(TuitionSection), "/tuition", true),
        new("privacy", "Privacy & cookies", "Pages", "The privacy and cookie policy.", typeof(TextSection), "/privacy", true),
        new("menu", "Main menu", "Layout", "Links in the slide-out navigation.", typeof(MenuSection), null, false),
        new("footer", "Footer", "Layout", "Logo, tagline and footer link lists.", typeof(FooterSection), null, false),
        new("bottomstrip", "Bottom strip", "Layout", "The copyright line at the very bottom.", typeof(BottomStripSection), null, false),
        new("general", "General settings", "Settings", "Logo, social links, contact form email and tracking.", typeof(GeneralSettings), null, false),
    ];

    public static Def? Find(string key) => All.FirstOrDefault(d => d.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
}
