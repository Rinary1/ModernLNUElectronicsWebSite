"""Генерує robots.txt і sitemap.xml для опублікованого сайту.

Запускається на деплої, коли вже відома справжня адреса (GitHub Pages віддає
сайт із /<repo>/, а локально це /), тому абсолютні URL зібрати можна лише тут.

    python tools/assets/generate_seo.py <wwwroot> <site-url>

Приклад:
    python tools/assets/generate_seo.py publish/wwwroot https://rinary1.github.io/ModernLNUElectronicsWebSite/
"""
from __future__ import annotations

import json
import os
import sys
import xml.etree.ElementTree as ET
from datetime import date
from urllib.parse import urlparse


def slugs_from_dir(root: str, folder: str) -> list[str]:
    path = os.path.join(root, "data", folder)
    if not os.path.isdir(path):
        return []
    return sorted(f[:-5] for f in os.listdir(path) if f.endswith(".json"))


def news_entries(root: str) -> list[tuple[str, str | None]]:
    path = os.path.join(root, "data", "news.json")
    if not os.path.isfile(path):
        return []
    with open(path, encoding="utf-8") as f:
        items = json.load(f)
    return [(i["slug"], (i.get("publishedAt") or "")[:10] or None) for i in items if i.get("slug")]


def collect(root: str) -> list[tuple[str, str | None, str]]:
    """(маршрут, lastmod, priority) для кожної сторінки дзеркала."""
    urls: list[tuple[str, str | None, str]] = [
        ("", None, "1.0"),
        ("news", None, "0.9"),
        ("schedule", None, "0.9"),
        ("staff", None, "0.8"),
        ("departments", None, "0.8"),
        ("about", None, "0.7"),
        ("administration", None, "0.6"),
        ("applicants", None, "0.7"),
        ("science", None, "0.6"),
        ("contacts", None, "0.6"),
    ]

    for slug, lastmod in news_entries(root):
        urls.append((f"news/{slug}", lastmod, "0.6"))

    for slug in slugs_from_dir(root, "departments"):
        urls.append((f"departments/{slug}", None, "0.6"))

    for slug in slugs_from_dir(root, "employees"):
        urls.append((f"staff/{slug}", None, "0.5"))

    # data/pages/<group>-<slug>.json -> /<group>/<slug>
    for name in slugs_from_dir(root, "pages"):
        group, _, slug = name.partition("-")
        if slug:
            urls.append((f"{group}/{slug}", None, "0.5"))

    return urls


def write_sitemap(root: str, site: str) -> int:
    ns = "http://www.sitemaps.org/schemas/sitemap/0.9"
    ET.register_namespace("", ns)
    urlset = ET.Element(f"{{{ns}}}urlset")
    today = date.today().isoformat()

    for route, lastmod, priority in collect(root):
        node = ET.SubElement(urlset, f"{{{ns}}}url")
        ET.SubElement(node, f"{{{ns}}}loc").text = site + route
        ET.SubElement(node, f"{{{ns}}}lastmod").text = lastmod or today
        ET.SubElement(node, f"{{{ns}}}priority").text = priority

    ET.ElementTree(urlset).write(
        os.path.join(root, "sitemap.xml"), encoding="utf-8", xml_declaration=True)
    return len(urlset)


def write_robots(root: str, site: str) -> None:
    """robots.txt для кореня домену.

    Увага: GitHub Pages віддає robots.txt лише з кореня домену. Для project-сайту
    (rinary1.github.io/<repo>/) краулери читають robots.txt сусіднього
    user-репозиторію, а не цей файл. Він стане робочим після переїзду на власний
    домен або в репозиторій <owner>.github.io; sitemap.xml працює й так - його
    можна віддати напряму в Search Console.
    """
    prefix = urlparse(site).path or "/"
    body = f"""# Неофіційне дзеркало сайту факультету електроніки ЛНУ.
# Першоджерело: https://electronics.lnu.edu.ua/ - на нього вказує rel="canonical"
# на всіх дзеркальних сторінках.

User-agent: *
Allow: /

# Дані дзеркала - службові JSON, у видачі їм робити нічого.
Disallow: {prefix}data/

Sitemap: {site}sitemap.xml
"""
    with open(os.path.join(root, "robots.txt"), "w", encoding="utf-8", newline="") as f:
        f.write(body)


def absolutize_index(root: str, site: str) -> None:
    """og:url та og:image мають бути абсолютними.

    Telegram і Facebook не резолвлять відносні шляхи проти <base href>,
    тому підставляємо повну адресу вже після publish.
    """
    path = os.path.join(root, "index.html")
    if not os.path.isfile(path):
        return

    with open(path, encoding="utf-8") as f:
        html = f.read()

    html = html.replace('content="og-image.png"', f'content="{site}og-image.png"')
    html = html.replace('property="og:url" content=""', f'property="og:url" content="{site}"')

    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(html)


def main() -> None:
    if len(sys.argv) != 3:
        sys.exit(__doc__)

    root, site = sys.argv[1], sys.argv[2]
    if not site.endswith("/"):
        site += "/"

    count = write_sitemap(root, site)
    write_robots(root, site)
    absolutize_index(root, site)
    print(f"sitemap.xml: {count} URL; robots.txt -> {site}")


if __name__ == "__main__":
    main()
