from pathlib import Path
from zipfile import ZipFile

from lxml import etree


SOURCE = Path(r"C:\808Music\artifacts\Prijedlog_teme_zavrsnog_rada_808Music_stem_separation.docx")
OUTPUT = Path(r"C:\808Music\artifacts\Prijedlog_teme_zavrsnog_rada_808Music_refaktorirana.docx")
PDF = Path(r"C:\808Music\artifacts\Prijedlog_teme_zavrsnog_rada_808Music_refaktorirana.pdf")
NS = {"w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"}


def load(path: Path):
    with ZipFile(path) as archive:
        bad_member = archive.testzip()
        root = etree.fromstring(archive.read("word/document.xml"))
    return bad_member, root


def text(node) -> str:
    return "".join(node.xpath(".//w:t/text()", namespaces=NS)).strip()


source_bad, source_root = load(SOURCE)
output_bad, output_root = load(OUTPUT)
assert source_bad is None and output_bad is None

source_body_paragraphs = source_root.xpath("./w:body/w:p", namespaces=NS)
output_body_paragraphs = output_root.xpath("./w:body/w:p", namespaces=NS)
assert [etree.tostring(p) for p in source_body_paragraphs[:11]] == [
    etree.tostring(p) for p in output_body_paragraphs[:11]
], "Prva stranica nije ostala identična izvornoj prijavi"

all_text = "\n".join(text(p) for p in output_root.xpath(".//w:p", namespaces=NS))
required = [
    "Improving Music Listening and Discovery with Machine Learning",
    "proširivu infrastrukturu klasifikacijskih glava",
    "eksplorativna studija slučaja",
    "bez korisničke usporedbe varijante s objašnjenjem i bez njega",
    "SDR/SIR/SAR izračunat će se samo ako postoje referentni izolirani stemovi",
    "Korisnička studija stem-playera IP5",
    "Web Audio API",
]
for fragment in required:
    assert fragment in all_text, f"Nedostaje obvezna formulacija: {fragment}"

for forbidden in [
    "audioanaliz",
    "problem „cold“ starta",
    "Klasifikacija će se evaluirati metrikama F1",
    "personalizirani popis bez objašnjenja",
    "multi-label“ klasifikator modernih glazbenih žanrova razvijen",
]:
    assert forbidden not in all_text, f"Pronađena zastarjela formulacija: {forbidden}"

tables = output_root.xpath(".//w:tbl", namespaces=NS)
assert len(tables) == 1
rows = tables[0].xpath("./w:tr", namespaces=NS)
assert len(rows) == 8
assert "Demucsov CPU/GPU benchmark" in text(rows[5])
assert "Korisnička studija stem-playera IP5" in text(rows[6])

assert PDF.exists() and PDF.stat().st_size > 0
print("VALIDATION=PASSED")
print(f"docx_bytes={OUTPUT.stat().st_size}")
print(f"pdf_bytes={PDF.stat().st_size}")
print(f"body_paragraphs={len(output_body_paragraphs)}")
print(f"tables={len(tables)}")
