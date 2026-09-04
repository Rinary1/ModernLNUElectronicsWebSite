from __future__ import annotations

import argparse
import json
import os
import re
import sys
import time
import urllib.request

import pdfplumber

USER_AGENT = "ModernLNUElectronicsMirror/1.0 (+https://github.com/Rinary1/ModernLNUElectronicsWebSite)"

DAYS = ["Понеділок", "Вівторок", "Середа", "Четвер", "П'ятниця", "Субота"]

BELLS = {
    "1": "08:30-09:50",
    "2": "10:10-11:30",
    "3": "11:50-13:10",
    "4": "13:30-14:50",
    "5": "15:05-16:25",
    "6": "16:45-18:05",
    "7": "18:20-19:40",
}

GROUP_CODE = re.compile(r"^[А-ЯІЇЄҐ]{2,5}[-–]?\s?\d{1,2}[а-яїієґ]?$", re.IGNORECASE)


def letters(value: str) -> str:
    return re.sub(r"[^\w]", "", (value or "").lower().replace("`", "").replace("'", "").replace("’", ""))


def match_day(cell: str) -> str | None:
    got = sorted(letters(cell))
    if len(got) < 5:
        return None

    for day in DAYS:
        if sorted(letters(day)) == got:
            return day

    return None


def clean(cell: str | None) -> str:
    return re.sub(r"\s+", " ", (cell or "").replace("\n", " ")).strip()


def is_header(cells: list[str]) -> int | None:
    weekly = len(cells) > 3 and "ден" in cells[0].lower() and "пар" in cells[1].lower()
    labels = 3 if weekly else 1

    body = cells[labels:]
    codes = [c for c in body if GROUP_CODE.match(c)]

    return labels if len(codes) >= 2 and len(codes) >= len(body) - 1 else None


def parse_pdf(path: str) -> dict | None:
    groups: list[str] = []
    labels = 3
    rows: list[dict] = []
    text_parts: list[str] = []

    day = None
    previous_pair = None

    with pdfplumber.open(path) as pdf:
        for page in pdf.pages:
            text_parts.append(page.extract_text() or "")

            for table in page.find_tables():
                for raw in table.extract():
                    cells = [clean(c) for c in raw]
                    if len(cells) < 2:
                        continue

                    if (found := is_header(cells)) is not None:
                        labels, groups = found, cells[found:]
                        continue

                    if not groups:
                        continue

                    body = cells[labels:labels + len(groups)]
                    body += [""] * (len(groups) - len(body))

                    if not any(body):
                        continue

                    if labels == 1:
                        rows.append({"label": cells[0], "day": "", "pair": "", "time": "", "cells": body})
                        continue

                    named = match_day(cells[0])
                    pair = cells[1] if cells[1].isdigit() else ""

                    if named:
                        day = named
                    elif pair == "1" and previous_pair not in (None, "1"):
                        day = DAYS[DAYS.index(day) + 1] if day in DAYS[:-1] else day

                    if pair:
                        previous_pair = pair

                    if not pair and rows:
                        rows[-1]["cells"] = [
                            "; ".join(p for p in (old, new) if p)
                            for old, new in zip(rows[-1]["cells"], body)
                        ]
                        continue

                    rows.append({
                        "label": "",
                        "day": day or "",
                        "pair": pair,
                        "time": BELLS.get(pair, ""),
                        "cells": body,
                    })

    if not groups or not rows:
        return None

    return {
        "kind": "weekly" if labels == 3 else "grid",
        "groups": groups,
        "rows": rows,
        "text": re.sub(r"\s+", " ", " ".join(text_parts)).strip(),
    }


def slug_of(url: str) -> str:
    name = os.path.basename(url.split("?")[0])
    return re.sub(r"[^A-Za-z0-9._-]", "-", os.path.splitext(name)[0])[:80]


def download(url: str, path: str) -> bool:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    try:
        with urllib.request.urlopen(request, timeout=60) as response, open(path, "wb") as f:
            f.write(response.read())
        return True
    except Exception as error:
        print(f"  ! не вдалося завантажити {url}: {error}")
        return False


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("data")
    parser.add_argument("--delay", type=float, default=0.5)
    parser.add_argument("--cache", default=None, help="Куди класти завантажені PDF")
    args = parser.parse_args()

    index_path = os.path.join(args.data, "schedule.json")
    if not os.path.isfile(index_path):
        sys.exit(f"немає {index_path} - спершу запустіть скрапер")

    with open(index_path, encoding="utf-8") as f:
        docs = json.load(f)

    out_dir = os.path.join(args.data, "schedule")
    os.makedirs(out_dir, exist_ok=True)
    cache = args.cache or os.path.join(args.data, ".pdf-cache")
    os.makedirs(cache, exist_ok=True)

    parsed = 0
    groups_index: list[dict] = []

    for doc in docs:
        doc = {k.lower(): v for k, v in doc.items()}
        url = doc["url"]
        slug = slug_of(url)
        pdf_path = os.path.join(cache, slug + ".pdf")

        if not os.path.isfile(pdf_path):
            print(f"pdf {url}")
            if not download(url, pdf_path):
                continue
            time.sleep(args.delay)

        try:
            table = parse_pdf(pdf_path)
        except Exception as error:
            print(f"  ! не вдалося розібрати {slug}: {error}")
            continue

        if table is None:
            print(f"  - {slug}: таблиці не знайдено (мабуть, скан або вільна верстка)")
            continue

        table["url"] = url
        table["title"] = doc.get("title", "")

        with open(os.path.join(out_dir, slug + ".json"), "w", encoding="utf-8") as f:
            json.dump(table, f, ensure_ascii=False, separators=(",", ":"))

        for column, group in enumerate(table["groups"]):
            if group:
                groups_index.append({
                    "group": group,
                    "file": slug,
                    "column": column,
                    "kind": table["kind"],
                    "title": table["title"],
                    "section": doc.get("section", ""),
                })

        parsed += 1
        print(f"  + {slug}: {len(table['groups'])} груп, {len(table['rows'])} рядків")

    groups_index.sort(key=lambda g: (g["group"], g["file"]))
    with open(os.path.join(args.data, "schedule-groups.json"), "w", encoding="utf-8") as f:
        json.dump(groups_index, f, ensure_ascii=False, separators=(",", ":"))

    print(f"розібрано {parsed} із {len(docs)}, груп у покажчику {len(groups_index)}")


if __name__ == "__main__":
    main()
