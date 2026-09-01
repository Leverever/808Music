from pathlib import Path

import pymupdf


pdf = Path(r"C:\808Music\artifacts\Prijedlog_teme_zavrsnog_rada_808Music_refaktorirana.pdf")
output = Path(r"C:\808Music\.tmp\application_review")
output.mkdir(parents=True, exist_ok=True)

document = pymupdf.open(pdf)
print(f"pages={document.page_count}")
for page_index, page in enumerate(document):
    image = page.get_pixmap(matrix=pymupdf.Matrix(1.5, 1.5), alpha=False)
    path = output / f"page_{page_index + 1:02d}.png"
    image.save(path)
    print(path)
