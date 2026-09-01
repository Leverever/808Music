# Sažetak izmjerenih rezultata

## Skup podataka

- Pjesme u katalogu: 181
- Pjesme s valjanim aktivnim vektorom značajki: 178 (98.3%)
- Dimenzionalnost vektora: 1280
- Aktivne audio-oznake: 2553
- Korisnici s interakcijama: 1
- Pozitivni događaji prema unaprijed zadanom pravilu: 12
- Valjane vremenske epizode za evaluaciju: 10

## Klasteriranje

Najbolji složeni rezultat ostvarila je konfiguracija `hdbscan:5`. Silhouette iznosi 0.492, semantička koherentnost 0.958, stabilnost ARI 1.000, a udio šuma 10.1%.

## Sustav preporuka

Za hibridnu varijantu na 10 vremenskih epizoda dobiveni su Recall@10=0.100, NDCG@10=0.039, MRR=0.025, pokrivenost kataloga=28.1% i prosječna raznolikost liste=0.332.

Ovi rezultati su eksplorativna studija slučaja jednog korisnika i ne predstavljaju procjenu kvalitete za populaciju korisnika.

## Cold-start novih pjesama

U katalogu je pronađeno 67 analiziranih pjesama bez interakcija i s nula streamova. Na 12 kontroliranih seed-slučajeva radio-preporuke vratile su prosječno 60.8% pjesama iz istog klastera, prosječni Jaccard audio-oznaka 0.513 i kosinusnu sličnost 0.687.

## Operativna pouzdanost

Audio-analiza pokriva 98.3% kataloga. Stopa uspješnosti završenih poslova audio-analize je 98.9%. Stopa uspješnosti završenih Demucs poslova je 98.4%; medijan trajanja je 45.0 s, a P95 70.0 s.

Od 183 spremnih stem setova, 183 sadrži točno četiri stem datoteke.

## Ograničenja

- Nema referentnih izoliranih stemova, pa se SDR/SIR/SAR ne može valjano izračunati.
- Povijesni poslovi ne bilježe korišteni uređaj, pa CPU/GPU usporedba zahtijeva zaseban kontrolirani benchmark.
- IP5 (korisničko ispitivanje stem reproduktora) namjerno nije proveden ovom skriptom.