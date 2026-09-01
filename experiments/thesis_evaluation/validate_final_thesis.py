from __future__ import annotations

import re
import sys
from pathlib import Path
from zipfile import ZipFile

from lxml import etree


DOCX = Path(r"C:\808Music\artifacts\zavrsni_v4_rezultati_final.docx")
PDF = Path(r"C:\808Music\artifacts\zavrsni_v4_rezultati_final.pdf")
W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
NS = {"w": W}


def node_text(node) -> str:
    return "".join(node.xpath(".//w:t/text()", namespaces=NS)).strip()


failures: list[str] = []
with ZipFile(DOCX) as archive:
    bad_member = archive.testzip()
    if bad_member:
        failures.append(f"Neispravan ZIP član: {bad_member}")
    root = etree.fromstring(archive.read("word/document.xml"))
    media = [name for name in archive.namelist() if name.startswith("word/media/")]

paragraphs = [node_text(node) for node in root.xpath(".//w:p", namespaces=NS)]
all_text = "\n".join(paragraphs)
tables = root.xpath(".//w:tbl", namespaces=NS)

expected_table_shapes = {
    "Komponenta|Naredba ili postupak|Rezultat": (4, 3),
    "Algoritam i parametri|Broj klastera|Šum": (4, 7),
    "Svojstvo|Vrijednost": (8, 2),
    "Model|Precision@10|Recall@10": (8, 7),
    "Varijanta|ΔNDCG@10|Δpokrivenost": (7, 6),
    "Izvor|Medijan SDR|Sredina SDR": (5, 5),
    "Uređaj|Profil|Broj poslova": (5, 7),
    "Preglednik/uređaj|Scenarij|Medijan odmaka": (5, 6),
    "Provjera|Status|Dokaz ili lokacija": (9, 3),
    "Tvrdnja, skala 1–5|Medijan|Interkvartilni raspon": (6, 3),
}
found_headers: set[str] = set()
for table in tables:
    rows = table.xpath("./w:tr", namespaces=NS)
    columns = rows[0].xpath("./w:tc", namespaces=NS) if rows else []
    headings = [node_text(cell) for cell in columns]
    header = "|".join(headings[:3])
    if header not in expected_table_shapes:
        continue
    expected = expected_table_shapes[header]
    actual = (len(rows), len(columns))
    if actual != expected:
        failures.append(f"Tablica '{header}': {actual}, očekivano {expected}")
    found_headers.add(header)
for header in expected_table_shapes.keys() - found_headers:
    failures.append(f"Nije pronađena očekivana tablica: {header}")

required_fragments = [
    "36 prošlo, 0 palo, 0 preskočeno",
    "HDBSCAN, min_cluster_size=5",
    "CONTENT-PROFILE",
    "N/P",
    "NVIDIA GeForce RTX 3060 Ti",
    "Headless Chrome 151 / Windows 10",
    "thesis-eval-20260807-v1",
    "10d815d810138e5ec433c8000f08dd0b556eb62bd055466d9ec3eff2a31b4bd8",
    "AUTOR MORA POTVRDITI",
]
for fragment in required_fragments:
    if fragment not in all_text:
        failures.append(f"Nedostaje obvezni tekst: {fragment}")

markers = [text for text in paragraphs if "[UNIJETI]" in text or "[DOPUNITI" in text]
allowed_marker_patterns = [
    re.compile(r"^\[UNIJETI\]$"),  # ten result cells in IP5 table 14
    re.compile(r"Sudjelovanje je dobrovoljno i traje približno \[UNIJETI\] minuta"),
    re.compile(r"Ispitanik dobiva \[UNIJETI\] glazbena isječka"),
    re.compile(r"^\[DOPUNITI: kontakt istraživača"),
    re.compile(r"^3\. Provesti IP5 i zamijeniti njegova preostala polja \[UNIJETI\]"),
]
unexpected_markers = [
    text for text in markers if not any(pattern.search(text) for pattern in allowed_marker_patterns)
]
if unexpected_markers:
    failures.extend(f"Neočekivani placeholder: {text}" for text in unexpected_markers)

exact_unijeti = sum(text == "[UNIJETI]" for text in paragraphs)
if exact_unijeti != 10:
    failures.append(f"IP5 tablica ima {exact_unijeti} polja [UNIJETI], očekivano 10")

if len(media) < 6:
    failures.append(f"DOCX ima samo {len(media)} medijskih datoteka; očekivano najmanje 6")

if not PDF.exists() or PDF.stat().st_size == 0:
    failures.append("PDF nije generiran ili je prazan")

print(f"docx_bytes={DOCX.stat().st_size}")
print(f"pdf_bytes={PDF.stat().st_size}")
print(f"paragraphs={len(paragraphs)}")
print(f"tables={len(tables)}")
print(f"media_files={len(media)}")
print(f"remaining_marker_paragraphs={len(markers)}")
for marker in markers:
    print(f"  marker: {marker}")

if failures:
    print("VALIDATION=FAILED")
    for failure in failures:
        print(f"  - {failure}")
    sys.exit(1)

print("VALIDATION=PASSED")
