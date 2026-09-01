from __future__ import annotations

from pathlib import Path

import pymupdf


PDF = Path(r"C:\808Music\artifacts\zavrsni_v4_rezultati_final.pdf")
OUTPUT = Path(r"C:\808Music\.tmp\pdf_review")
OUTPUT.mkdir(parents=True, exist_ok=True)

targets = [
    "6. Testiranje i prikaz rezultata",
    "Tablica 6. Rezultati dostupnih automatiziranih tehničkih provjera",
    "Tablica 7. Usporedba algoritama grupiranja audio-ugradnji",
    "Tablica 9. Glavne metrike rangiranja pri K = 10",
    "Tablica 10. Analiza uklanjanja signala iz hibridnog modela",
    "Tablica 12. CPU/GPU izvedba separacijskog toka",
    "Tablica 13. Mjerenje sinkronizacije stemova u pregledniku",
    "Tablica 14. Sažetak korisničke evaluacije stem-playera",
    "Prilog B. Kontrolna lista prije pokretanja eksperimenata",
    "Prilog E. Predložak zapisa eksperimenta",
]

document = pymupdf.open(PDF)
locations: dict[str, int] = {}
for page_index, page in enumerate(document):
    text = " ".join(page.get_text("text").split())
    for target in targets:
        # Keep the last occurrence so entries in the table of contents do not
        # replace the physical chapter/caption page.
        if target in text:
            locations[target] = page_index

print(f"pages={document.page_count}")
for target in targets:
    page_index = locations.get(target)
    print(f"{page_index + 1 if page_index is not None else 'NOT_FOUND'}|{target}")

review_pages = sorted(set(locations.values()) | set(range(58, 67)))
matrix = pymupdf.Matrix(1.7, 1.7)
for page_index in review_pages:
    pixmap = document[page_index].get_pixmap(matrix=matrix, alpha=False)
    output = OUTPUT / f"page_{page_index + 1:03d}.png"
    pixmap.save(output)
    print(output)
