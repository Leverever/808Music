# Reproducibilna evaluacija završnog rada

Ovaj direktorij sadrži skripte i rezultate tehničkih ispitivanja opisanih u
6. poglavlju završnog rada. Izvorni podaci ostaju u lokalnoj razvojnoj bazi;
u `data/` se izvoze samo zapisi potrebni za izračun metrika.

Ispitivanje korisničkog iskustva stem reproduktora (IP5) nije dio ovih skripti.
Njegove rezultate autor rada treba unijeti nakon provođenja ispitivanja s
korisnicima.

Pokretanje:

1. `powershell -ExecutionPolicy Bypass -File .\export_data.ps1`
2. `python .\evaluate.py`

Rezultati se zapisuju u `results/`, a grafički prilozi u `figures/`.
