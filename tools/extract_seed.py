#!/usr/bin/env python3
"""
Builds Cms.Api/Seed/*.json from the existing Razor page (Pages/Index.cshtml).

The CMS uses these files the first time it starts, so the dashboard opens with the content that is
already on the live site. Re-run it only if you want to regenerate the starting content:

    python3 tools/extract_seed.py path/to/Pages/Index.cshtml Cms.Api/Seed

Needs: pip install beautifulsoup4
"""
import html, json, re, sys
from pathlib import Path
from bs4 import BeautifulSoup

src = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("Pages/Index.cshtml")
out = Path(sys.argv[2]) if len(sys.argv) > 2 else Path("Cms.Api/Seed")
out.mkdir(parents=True, exist_ok=True)

raw = src.read_text(encoding="utf-8-sig")
raw = raw.replace("@@", "@").replace("~/Images/", "/images/").replace("~/", "/")
soup = BeautifulSoup(raw, "html.parser")


def write(key, data):
    (out / f"{key}.json").write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print("wrote", key)


def inner(el):
    return "".join(str(c) for c in el.contents).strip()


def clean_text(el):
    return re.sub(r"\s+", " ", el.get_text(" ", strip=True)).strip()


def html_of(*els):
    return "".join(f"<p>{inner(e)}</p>" if e.name == "p" else str(e) for e in els)


def one_line(html):
    return re.sub(r">\s+<", "><", re.sub(r"\s+", " ", html)).strip()


# meta-map JSON (per-section title / description / share image)
meta = json.loads(html.unescape(soup.find("template", id="meta-map").decode_contents()))
SHARE_OK = {"/images/hero/hero-1-4k.webp", "/images/hero/hero-2-4k.webp"}  # the other image paths in the old map do not exist


def seo(section_id):
    m = meta[section_id]
    img = re.sub(r"^https?://[^/]+", "", m.get("image", ""))
    return {"title": m["title"], "description": m["description"], "image": img if img in SHARE_OK else ""}


def img_src(img):
    return img["src"]


# ------------------------------------------------------------------ hero
hero = soup.find("section", id="hero")
slides = [{"image": b.find("img")["src"], "alt": b.find("img").get("alt", "")} for b in hero.select(".banner")]
h1 = hero.find("h1")
write("hero", {
    "slides": slides,
    "headingLead": h1.find(string=True, recursive=False).strip(),
    "headingHighlight": clean_text(h1.find("span", class_="highlight")),
    "text": "<p>" + one_line(inner(hero.select_one(".hero-left p"))) + "</p>",
    "showSocialIcons": True,
    "autoplaySeconds": 4,
    "seo": seo("hero"),
})

# ------------------------------------------------------------------ about
about = soup.find("section", id="about")
write("about", {
    "imageCard": {"image": "", "alt": "", "caption": "BIGGI\nJUNIOR\nSCHOOL"},
    "eyebrow": clean_text(about.select_one(".eyebrow")),
    "heading": clean_text(about.find("h2")),
    "text": one_line(html_of(*about.select(".about-text > p"))),
    "stats": [{"value": clean_text(d.find("strong")), "label": clean_text(d.find("span"))} for d in about.select(".stat-row > div")],
    "seo": seo("about"),
})

# ------------------------------------------------------------------ leadership
prof = soup.find("section", id="profiles")
people = []
for card in prof.select(".p-card"):
    pid = card["data-bio-id"]
    tpl = soup.find("template", id=f"bio-{pid}")
    tpl_soup = BeautifulSoup(tpl.decode_contents(), "html.parser")
    paras = tpl_soup.find_all("p")
    name = clean_text(card.select_one(".p-strip b"))
    people.append({
        "id": pid,
        "image": card.find("img")["src"],
        "name": name,
        "designation": clean_text(card.select_one(".p-strip span")),
        "bio": {"title": clean_text(tpl_soup.find("h3")) or name, "text": one_line(html_of(*paras)).replace("&amp;amp;", "&amp;")},
    })
write("leadership", {
    "eyebrow": clean_text(prof.select_one(".eyebrow")),
    "heading": clean_text(prof.find("h2")),
    "people": people,
    "seo": seo("profiles"),
})

# ------------------------------------------------------------------ history
hist = soup.find("section", id="history")
write("history", {
    "eyebrow": clean_text(hist.select_one(".eyebrow")),
    "heading": clean_text(hist.find("h2")),
    "text": one_line(html_of(*hist.select(".history > p"))),
    "seo": seo("history"),
})


# ------------------------------------------------------------------ galleries
ICONS = {  # first characters of each svg path -> icon name used by the website
    "M12 3 2 11": "home", "M12 3 1 8": "cap", "M7 2h2v2h6": "calendar", "M12 2a10 10": "ball", "M4 4.5A1.5": "book",
    "M4 4h16a1": "chat", "M12 2a7 7": "netball", "M6 2v8.5": "dining", "M9 17a3 3": "music", "M14.1 3a1.7": "run", "M9 2h6v2": "flask",
}


def icon_for(box):
    d = box.find("path")["d"]
    for prefix, name in ICONS.items():
        if d.startswith(prefix):
            return name
    return "home"


def gallery(section_id, key):
    sec = soup.find("section", id=section_id)
    labels = {b["data-f"].lstrip("."): clean_text(b) for b in sec.select(".filters button") if b["data-f"] != "*"}
    all_label = clean_text(sec.select_one(".filters button[data-f='*']"))
    cards = []
    for item in sec.select(".iso-item"):
        box = item.select_one(".box")
        cards.append({
            "id": re.sub(r"[^a-z0-9]+", "-", box["data-label"].lower()).strip("-"),
            "title": box["data-label"],
            "type": labels.get(item["data-cat"], item["data-cat"].title()),
            "icon": icon_for(box),
            "images": [{"src": i["src"], "alt": i.get("alt", "")} for i in item.select(".iso-images img")],
        })
    cta_a = sec.select_one(".cta-row a")
    head_p = sec.select_one(".sect-head p")
    return {
        "eyebrow": clean_text(sec.select_one(".eyebrow")),
        "heading": clean_text(sec.find("h2")),
        "intro": f"<p>{inner(head_p)}</p>" if head_p else "",
        "allLabel": all_label,
        "cards": cards,
        "cta": {"label": clean_text(cta_a), "url": cta_a["href"], "newTab": True} if cta_a else {"label": "", "url": "", "newTab": True},
        "seo": seo(section_id),
    }


write("socialwall", gallery("gallery", "socialwall"))
write("activities", gallery("activities", "activities"))

# ------------------------------------------------------------------ contact
contact = soup.find("section", id="contact")
details = contact.select_one(".contact-details")
socials = {}
phones = []
for p in details.select(".contact-socials p"):
    label = p.get_text(" ", strip=True)
    links = p.find_all("a")
    if label.startswith("Tel"):
        phones = [clean_text(a) for a in links]
    elif label.startswith("Email"):
        socials["email"] = clean_text(links[0])
    else:
        key = label.split(":")[0].strip().lower()
        socials[key] = {"label": clean_text(links[0]), "url": links[0]["href"]}
address = details.find("p", recursive=False)
write("contact", {
    "eyebrow": clean_text(contact.select_one(".eyebrow")),
    "heading": clean_text(contact.select(".sect-head h2")[0]),
    "mapEmbedUrl": contact.select_one(".map-embed-full iframe")["src"],
    "details": {
        "title": clean_text(details.find("h3")),
        "address": "\n".join(s.strip() for s in address.get_text("\n").split("\n") if s.strip()),
        "phones": phones,
        "email": socials["email"],
        "youTube": socials["youtube"],
        "facebook": socials["facebook"],
        "whatsApp": socials["whatsapp"],
    },
    "seo": seo("contact"),
})

# ------------------------------------------------------------------ tuition
tuition = soup.find("section", id="tuition")
bank = soup.find("template", id="bank-details")
bank_soup = BeautifulSoup(bank.decode_contents(), "html.parser")
bank_html = "<p>" + inner(bank_soup.find("p")) + "</p>"
for row in bank_soup.select(".bank-details > div"):
    bank_html += f"<p><strong>{clean_text(row.find('span'))}</strong> {clean_text(row.find('b'))}</p>"
tuition_html = html_of(*tuition.select(".donate-box > p")) + "<ul>" + "".join(f"<li>{inner(li)}</li>" for li in tuition.select(".donate-list li")) + "</ul>"
write("tuition", {
    "eyebrow": clean_text(tuition.select_one(".eyebrow")),
    "heading": clean_text(tuition.find("h2")),
    "text": one_line(tuition_html),
    "button": {"label": clean_text(tuition.select_one("#bank-btn")), "action": "panel", "url": "", "newTab": False},
    "panel": {"title": clean_text(bank_soup.find("h3")), "text": one_line(bank_html)},
    "seo": seo("tuition"),
})

# ------------------------------------------------------------------ privacy
priv = soup.find("section", id="privacy")
parts = [str(e) if e.name != "p" else f"<p>{inner(e)}</p>" for e in priv.select(".legal > p, .legal > h4")]
write("privacy", {
    "eyebrow": clean_text(priv.select_one(".eyebrow")),
    "heading": clean_text(priv.find("h2")),
    "text": one_line("".join(parts)),
    "seo": seo("privacy"),
})

# ------------------------------------------------------------------ footer / strip / menu
foot = soup.find("footer")
cols = foot.select(".foot-grid > div")[1:]


def links(col):
    result = []
    for a in col.find_all("a"):
        result.append({"label": clean_text(a), "url": a["href"], "newTab": a.get("target") == "_blank"})
    return result


write("footer", {
    "logo": "",
    "tagline": clean_text(foot.select_one(".foot-brand p")),
    "explore": {"title": clean_text(cols[0].find("h5")), "links": links(cols[0])},
    "connect": {"title": clean_text(cols[1].find("h5")), "links": links(cols[1])},
})
strip = foot.select(".foot-bottom span")
write("bottomstrip", {
    "left": clean_text(strip[0]).replace("© 2026", "© {year}"),
    "right": clean_text(strip[1]),
})
write("menu", {"items": [{"label": re.sub(r"^\d+\s*", "", clean_text(a)), "url": a["href"], "newTab": False} for a in soup.select("#navpanel a")]})

# ------------------------------------------------------------------ general
hero_social = {}
for a in hero.select("#sm-icons a"):
    h = a["href"]
    if "facebook" in h: hero_social["facebook"] = h
    elif "whatsapp" in h: hero_social["whatsapp"] = h
    elif "youtube" in h: hero_social["youtube"] = h
    elif h.startswith("mailto:"): hero_social["email"] = h[7:]
cookie = soup.find(id="cookies-directive").find("p")
gtm = re.search(r"GTM-[A-Z0-9]+", raw)
write("general", {
    "siteName": "Biggi Junior School",
    "tagline": "Education for a brighter future",
    "logo": "/images/logo_bjs_white.png",
    "logoDark": "/images/logo_bjs.png",
    "defaultShareImage": "/images/hero/hero-1-4k.webp",
    "social": [{"platform": p, "url": hero_social[p]} for p in ("facebook", "whatsapp", "youtube") if p in hero_social],
    "publicEmail": hero_social.get("email", ""),
    "notificationEmail": hero_social.get("email", ""),
    "notificationSubject": "Online enquiry",
    "cookieNoticeEnabled": True,
    "cookieNoticeText": cookie.find(string=True, recursive=False).strip(),
    "cookieLinkLabel": clean_text(cookie.find("a")),
    "gtmId": gtm.group(0) if gtm else "",
})
