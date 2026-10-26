namespace Bjs.Cms.Contracts;

// ---------------------------------------------------------------------------
// Shared building blocks
// ---------------------------------------------------------------------------

/// <summary>Meta data for a page/section: browser title, search description and optional share image.</summary>
public class Seo
{
    [Max(70)] public string Title { get; set; } = "";
    [Max(200)] public string Description { get; set; } = "";
    /// <summary>Image shown when the link is shared (Facebook, WhatsApp...). Falls back to the site default.</summary>
    [MediaUrl, Max(500)] public string Image { get; set; } = "";
}

public class Link
{
    [Need, Max(80)] public string Label { get; set; } = "";
    [Need, LinkUrl, Max(500)] public string Url { get; set; } = "";
    public bool NewTab { get; set; }
}

/// <summary>A call-to-action button that is only shown when it has a label.</summary>
public class Cta
{
    [Max(80)] public string Label { get; set; } = "";
    [LinkUrl, Max(500)] public string Url { get; set; } = "";
    public bool NewTab { get; set; } = true;
}

public class ImageRef
{
    [Need, MediaUrl, Max(500)] public string Src { get; set; } = "";
    [Max(200)] public string Alt { get; set; } = "";
}

/// <summary>Display text plus link, e.g. "@BiggiJuniorSchool" -> facebook.com/...</summary>
public class LabelledLink
{
    [Max(120)] public string Label { get; set; } = "";
    [LinkUrl, Max(500)] public string Url { get; set; } = "";
}

// ---------------------------------------------------------------------------
// 1. Homepage hero
// ---------------------------------------------------------------------------

public class HeroSlide
{
    [Need, MediaUrl, Max(500)] public string Image { get; set; } = "";
    [Max(200)] public string Alt { get; set; } = "";
}

public class HeroSection
{
    [MaxItems(12)] public List<HeroSlide> Slides { get; set; } = new();
    /// <summary>First part of the heading, shown in white. e.g. "Education for a"</summary>
    [Max(100)] public string HeadingLead { get; set; } = "";
    /// <summary>Second part of the heading, shown in gold. e.g. "brighter future"</summary>
    [Max(100)] public string HeadingHighlight { get; set; } = "";
    [Html, Max(2000)] public string Text { get; set; } = "";
    public bool ShowSocialIcons { get; set; } = true;
    [IntRange(2, 30)] public int AutoplaySeconds { get; set; } = 4;
    public Seo Seo { get; set; } = new();
}

// ---------------------------------------------------------------------------
// 2. About
// ---------------------------------------------------------------------------

public class ImageCard
{
    [MediaUrl, Max(500)] public string Image { get; set; } = "";
    [Max(200)] public string Alt { get; set; } = "";
    /// <summary>Text on the card when no image is chosen (and used as a fallback caption).</summary>
    [Multiline, Max(80)] public string Caption { get; set; } = "";
}

public class Stat
{
    [Need, Max(24)] public string Value { get; set; } = "";
    [Max(60)] public string Label { get; set; } = "";
}

public class AboutSection
{
    public ImageCard ImageCard { get; set; } = new();
    [Max(80)] public string Eyebrow { get; set; } = "";
    [Max(160)] public string Heading { get; set; } = "";
    [Html, Max(8000)] public string Text { get; set; } = "";
    [MaxItems(6)] public List<Stat> Stats { get; set; } = new();
    public Seo Seo { get; set; } = new();
}

// ---------------------------------------------------------------------------
// 3. Leadership
// ---------------------------------------------------------------------------

public class Bio
{
    [Max(120)] public string Title { get; set; } = "";
    [Html, Max(6000)] public string Text { get; set; } = "";
}

public class Person
{
    /// <summary>Stable id used for the pop-out panel. Generated from the name when empty.</summary>
    [Max(80)] public string Id { get; set; } = "";
    [MediaUrl, Max(500)] public string Image { get; set; } = "";
    [Need, Max(100)] public string Name { get; set; } = "";
    [Max(100)] public string Designation { get; set; } = "";
    public Bio Bio { get; set; } = new();
}

public class LeadershipSection
{
    [Max(80)] public string Eyebrow { get; set; } = "";
    [Max(160)] public string Heading { get; set; } = "";
    [MaxItems(40)] public List<Person> People { get; set; } = new();
    public Seo Seo { get; set; } = new();
}

// ---------------------------------------------------------------------------
// 4 + 9. Text pages (History, Privacy & Cookies)
// ---------------------------------------------------------------------------

public class TextSection
{
    [Max(80)] public string Eyebrow { get; set; } = "";
    [Max(160)] public string Heading { get; set; } = "";
    [Html, Max(30000)] public string Text { get; set; } = "";
    public Seo Seo { get; set; } = new();
}

// ---------------------------------------------------------------------------
// 5 + 6. Galleries (Social wall, Activities & Facilities)
// ---------------------------------------------------------------------------

public class GalleryCard
{
    [Max(80)] public string Id { get; set; } = "";
    [Need, Max(80)] public string Title { get; set; } = "";
    /// <summary>Category used by the filter buttons, e.g. "Events". Cards with the same type are grouped.</summary>
    [Max(40)] public string Type { get; set; } = "";
    /// <summary>Name of the icon shown on the card tile (see GalleryIcons).</summary>
    [Max(30)] public string Icon { get; set; } = "";
    [MaxItems(60)] public List<ImageRef> Images { get; set; } = new();
}

public class GallerySection
{
    [Max(80)] public string Eyebrow { get; set; } = "";
    [Max(160)] public string Heading { get; set; } = "";
    [Html, Max(2000)] public string Intro { get; set; } = "";
    /// <summary>Label of the first filter button (shows every card).</summary>
    [Max(30)] public string AllLabel { get; set; } = "All";
    [MaxItems(80)] public List<GalleryCard> Cards { get; set; } = new();
    public Cta Cta { get; set; } = new();
    public Seo Seo { get; set; } = new();
}

public static class GalleryIcons
{
    public const string Default = "home";
    public static readonly string[] Names =
        ["home", "cap", "calendar", "ball", "book", "chat", "netball", "dining", "music", "run", "flask", "camera"];
}

// ---------------------------------------------------------------------------
// 7. Get in touch
// ---------------------------------------------------------------------------

public class ContactDetails
{
    [Max(120)] public string Title { get; set; } = "";
    [Multiline, Max(400)] public string Address { get; set; } = "";
    [MaxItems(4)] public List<string> Phones { get; set; } = new();
    [Max(200)] public string Email { get; set; } = "";
    public LabelledLink YouTube { get; set; } = new();
    public LabelledLink Facebook { get; set; } = new();
    public LabelledLink WhatsApp { get; set; } = new();
}

public class ContactSection
{
    [Max(80)] public string Eyebrow { get; set; } = "";
    [Max(160)] public string Heading { get; set; } = "";
    /// <summary>Google Maps "Embed a map" link. A pasted &lt;iframe&gt; snippet is accepted and reduced to its link.</summary>
    [EmbedUrl, Max(2000)] public string MapEmbedUrl { get; set; } = "";
    public ContactDetails Details { get; set; } = new();
    public Seo Seo { get; set; } = new();
}

// ---------------------------------------------------------------------------
// 8. Tuition & Fees
// ---------------------------------------------------------------------------

public class TuitionButton
{
    [Max(80)] public string Label { get; set; } = "";
    /// <summary>"link" opens Url; "panel" slides open the panel below.</summary>
    [Max(10)] public string Action { get; set; } = "link";
    [LinkUrl, Max(500)] public string Url { get; set; } = "";
    public bool NewTab { get; set; }
}

public class SlidePanel
{
    [Max(120)] public string Title { get; set; } = "";
    [Html, Max(6000)] public string Text { get; set; } = "";
}

public class TuitionSection
{
    [Max(80)] public string Eyebrow { get; set; } = "";
    [Max(160)] public string Heading { get; set; } = "";
    [Html, Max(10000)] public string Text { get; set; } = "";
    public TuitionButton Button { get; set; } = new();
    /// <summary>Pop-out panel opened by the button when Action is "panel" (bank details, etc.).</summary>
    public SlidePanel Panel { get; set; } = new();
    public Seo Seo { get; set; } = new();
}

// ---------------------------------------------------------------------------
// 10 + 11. Footer and bottom strip
// ---------------------------------------------------------------------------

public class FooterMenu
{
    [Max(60)] public string Title { get; set; } = "";
    [MaxItems(20)] public List<Link> Links { get; set; } = new();
}

public class FooterSection
{
    /// <summary>Footer logo. Leave empty to use the site logo from General settings.</summary>
    [MediaUrl, Max(500)] public string Logo { get; set; } = "";
    [Multiline, Max(400)] public string Tagline { get; set; } = "";
    public FooterMenu Explore { get; set; } = new() { Title = "Explore" };
    public FooterMenu Connect { get; set; } = new() { Title = "Connect" };
}

public class BottomStripSection
{
    /// <summary>Use {year} to insert the current year automatically.</summary>
    [Max(200)] public string Left { get; set; } = "";
    [Max(200)] public string Right { get; set; } = "";
}

// ---------------------------------------------------------------------------
// 12. Main menu
// ---------------------------------------------------------------------------

public class MenuItem
{
    [Need, Max(60)] public string Label { get; set; } = "";
    /// <summary>Internal ("/about", "/#contact") or external ("https://...") address.</summary>
    [Need, LinkUrl, Max(500)] public string Url { get; set; } = "";
    public bool NewTab { get; set; }
}

public class MenuSection
{
    [MaxItems(20)] public List<MenuItem> Items { get; set; } = new();
}

// ---------------------------------------------------------------------------
// General settings
// ---------------------------------------------------------------------------

public static class SocialPlatforms
{
    public static readonly string[] All = ["facebook", "youtube", "whatsapp", "x", "twitter", "tiktok", "linkedin", "instagram"];
}

public class SocialLink
{
    [Need, Max(20)] public string Platform { get; set; } = "";
    [Need, LinkUrl, Max(500)] public string Url { get; set; } = "";
}

public class GeneralSettings
{
    [Max(100)] public string SiteName { get; set; } = "";
    [Max(200)] public string Tagline { get; set; } = "";
    /// <summary>Main logo (the light/white version used on dark backgrounds: top bar, hero, footer).</summary>
    [MediaUrl, Max(500)] public string Logo { get; set; } = "";
    /// <summary>Optional logo for light backgrounds.</summary>
    [MediaUrl, Max(500)] public string LogoDark { get; set; } = "";
    /// <summary>Share image used when a section has none of its own.</summary>
    [MediaUrl, Max(500)] public string DefaultShareImage { get; set; } = "";

    [MaxItems(10)] public List<SocialLink> Social { get; set; } = new();
    /// <summary>Public address behind the mail icon in the hero.</summary>
    [Max(200)] public string PublicEmail { get; set; } = "";

    /// <summary>Where messages from the contact form are delivered. Never exposed publicly.</summary>
    [Max(200)] public string NotificationEmail { get; set; } = "";
    [Max(120)] public string NotificationSubject { get; set; } = "Online enquiry";

    public bool CookieNoticeEnabled { get; set; } = true;
    [Max(400)] public string CookieNoticeText { get; set; } = "";
    [Max(40)] public string CookieLinkLabel { get; set; } = "";

    /// <summary>Google Tag Manager container id, e.g. GTM-XXXXXXX. Leave empty to switch tracking off.</summary>
    [Max(20)] public string GtmId { get; set; } = "";
}

/// <summary>Subset of General settings that is safe to publish.</summary>
public class PublicGeneral
{
    public string SiteName { get; set; } = "";
    public string Tagline { get; set; } = "";
    public string Logo { get; set; } = "";
    public string LogoDark { get; set; } = "";
    public string DefaultShareImage { get; set; } = "";
    public List<SocialLink> Social { get; set; } = new();
    public string PublicEmail { get; set; } = "";
    public bool CookieNoticeEnabled { get; set; }
    public string CookieNoticeText { get; set; } = "";
    public string CookieLinkLabel { get; set; } = "";
    public string GtmId { get; set; } = "";
}

// ---------------------------------------------------------------------------
// What the public website receives
// ---------------------------------------------------------------------------

public class PublicContent
{
    /// <summary>Changes whenever anything is saved. Used as an ETag.</summary>
    public string Version { get; set; } = "";
    public DateTime GeneratedAtUtc { get; set; }
    /// <summary>Base address of the public website, used for canonical and share URLs.</summary>
    public string SiteUrl { get; set; } = "";
    public PublicGeneral General { get; set; } = new();
    public MenuSection Menu { get; set; } = new();
    public HeroSection Hero { get; set; } = new();
    public AboutSection About { get; set; } = new();
    public LeadershipSection Leadership { get; set; } = new();
    public TextSection History { get; set; } = new();
    public GallerySection SocialWall { get; set; } = new();
    public GallerySection Activities { get; set; } = new();
    public ContactSection Contact { get; set; } = new();
    public TuitionSection Tuition { get; set; } = new();
    public TextSection Privacy { get; set; } = new();
    public FooterSection Footer { get; set; } = new();
    public BottomStripSection BottomStrip { get; set; } = new();
}

/// <summary>Body of POST /api/public/contact.</summary>
public class EnquiryRequest
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Message { get; set; } = "";
    /// <summary>Honeypot. Real visitors leave this empty.</summary>
    public string? HpToken { get; set; }
}
