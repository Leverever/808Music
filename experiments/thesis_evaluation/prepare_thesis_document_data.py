from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parent
RESULTS = ROOT / "results"
FIGURES = ROOT / "figures"


def fmt(value: float, digits: int = 3) -> str:
    return f"{value:.{digits}f}".replace(".", ",")


def pct(value: float, digits: int = 1) -> str:
    return f"{value * 100:.{digits}f} %".replace(".", ",")


def delta(value: float, percentage_points: bool = False) -> str:
    scaled = value * 100 if percentage_points else value
    suffix = " p. b." if percentage_points else ""
    return f"{scaled:+.3f}{suffix}".replace(".", ",")


def seconds(value: float) -> str:
    return f"{value:.2f} s".replace(".", ",")


def mb(value: float) -> str:
    return f"{value:.2f} MB".replace(".", ",")


def main():
    final = json.loads((RESULTS / "final_results.json").read_text(encoding="utf-8"))
    dataset = final["dataset"]
    selected = final["clustering"]["selected"]
    recs = final["recommendations"]["table"]
    hybrid = recs["HYBRID"]
    content_profile = recs["CONTENT-PROFILE"]
    content_seed = recs["CONTENT-SEED"]
    operational = final["operational"]
    storage = final["storage"]
    cold = final["cold_start"]
    api = final["api"]
    answers = final["research_answers"]

    cluster_rows = [[
        "Algoritam i parametri",
        "Broj klastera",
        "Šum",
        "Silueta",
        "Davies–Bouldin",
        "Stabilnost",
        "Vrijeme",
    ]]
    cluster_labels = {
        "kmeans": lambda row: f"K-means, K={int(row['parameter'])}",
        "agglomerative": lambda row: f"Aglomerativno, K={int(row['parameter'])}, ward",
        "hdbscan": lambda row: f"HDBSCAN, min_cluster_size={int(row['parameter'])}",
    }
    representatives = {row["algorithm"]: row for row in final["clustering"]["representatives"]}
    for algorithm in ("kmeans", "agglomerative", "hdbscan"):
        row = representatives[algorithm]
        cluster_rows.append([
            cluster_labels[algorithm](row),
            str(int(row["cluster_count"])),
            pct(row["noise_ratio"]),
            fmt(row["silhouette_cosine"]),
            fmt(row["davies_bouldin"]),
            fmt(row["stability_ari"]),
            f"{row['fit_time_seconds'] * 1000:.0f} ms",
        ])

    recommendation_rows = [[
        "Model",
        "Precision@10",
        "Recall@10",
        "NDCG@10",
        "MRR",
        "Pokrivenost",
        "Intra-list raznolikost",
    ]]
    for label in ("POP", "FRESH-POP", "CONTENT-SEED", "CONTENT-PROFILE", "TAG-CLUSTER", "HYBRID-NODIV", "HYBRID"):
        row = recs[label]
        recommendation_rows.append([
            label,
            fmt(row["precision_at_10"]),
            fmt(row["recall_at_10"]),
            fmt(row["ndcg_at_10"]),
            fmt(row["mrr"]),
            pct(row["catalog_coverage_at_10"]),
            fmt(row["intra_list_diversity"]),
        ])

    ablation_rows = [[
        "Varijanta",
        "ΔNDCG@10",
        "Δpokrivenost",
        "Δraznolikost",
        "Cold-start korisnici",
        "Cold-start pjesme",
    ]]
    cold_item_notes = {
        "bez audio-ugradnje": "DA, preko oznaka/klastera",
        "bez oznaka": "DA, preko ugradnje/klastera",
        "bez klastera": "DA, preko ugradnje/oznaka",
        "bez vremenskog slabljenja": "DA",
        "bez novosti": "DA",
        "bez ograničenja raznolikosti": "DA",
    }
    for label, row in final["recommendations"]["ablations"].items():
        ablation_rows.append([
            label,
            delta(row["delta_ndcg_at_10"]),
            delta(row["delta_coverage_at_10"], True),
            delta(row["delta_diversity"]),
            "DA, uz fallback/seed",
            cold_item_notes[label],
        ])

    groups = {
        (row["device"], row["profile"]): row
        for row in final["demucs_benchmark"]["groups"]
    }
    gpu_name = final["demucs_benchmark"]["gpu"]
    demucs_rows = [[
        "Uređaj",
        "Profil",
        "Broj poslova",
        "Medijan vremena",
        "95. percentil",
        "Uspješnost",
        "Medijan izlaza",
    ]]
    for device, profile, device_label in (
        ("cpu", "four-stem", "CPU x86-64"),
        ("cuda", "four-stem", f"GPU {gpu_name}"),
        ("cpu", "two-stem-vocals", "CPU x86-64"),
        ("cuda", "two-stem-vocals", f"GPU {gpu_name}"),
    ):
        row = groups[(device, profile)]
        demucs_rows.append([
            device_label,
            "four-stem" if profile == "four-stem" else "two-stem",
            str(row["jobs"]),
            seconds(row["median_seconds"]),
            seconds(row["p95_seconds"]),
            pct(row["success_rate"]),
            mb(row["median_output_mb"]),
        ])

    sync_rows = [[
        "Preglednik/uređaj",
        "Scenarij",
        "Medijan odmaka",
        "95. percentil",
        "Maksimum",
        "Korekcije na sat",
    ]]
    for row in final["stem_sync"]["scenarios"][:4]:
        sync_rows.append([
            "Headless Chrome 151 / Windows 10",
            row["scenario"],
            f"{row['medianDriftMs']:.2f} ms".replace(".", ","),
            f"{row['p95DriftMs']:.2f} ms".replace(".", ","),
            f"{row['maximumDriftMs']:.2f} ms".replace(".", ","),
            fmt(row["correctionsPerHour"], 0),
        ])

    summary_result_hr = (
        f"Evaluacija je provedena nad katalogom od {dataset['catalog_tracks']} pjesme, od kojih "
        f"{dataset['analyzed_tracks']} ({pct(dataset['analysis_coverage'])}) ima aktivnu audio-analizu. "
        f"HDBSCAN s parametrom min_cluster_size=5 dao je tri klastera, {pct(selected['noise_ratio'])} šuma "
        f"i siluetu {fmt(selected['silhouette_cosine'])}. Rolling-origin evaluacija preporuka obuhvatila je "
        f"deset epizoda jednoga korisnika. Hibrid je ostvario NDCG@10={fmt(hybrid['ndcg_at_10'])}, "
        f"Recall@10={fmt(hybrid['recall_at_10'])} i pokrivenost {pct(hybrid['catalog_coverage_at_10'])}; "
        f"nije nadmašio CONTENT-PROFILE po relevantnosti, čiji je NDCG@10 iznosio {fmt(content_profile['ndcg_at_10'])}. "
        f"Za {cold['analyzed_items_without_interactions_and_streams']} analiziranih cold-start pjesama sadržajni radio vratio je "
        f"prosječno {pct(cold['same_cluster_at_10'])} rezultata iz istoga klastera. Od {operational['stem_jobs']} Demucs poslova "
        f"{operational['stem_ready']} je završilo uspješno, a svih {storage['unique_stem_keys']} stem objekata bilo je dohvatljivo. "
        "U browser-scenarijima P95 odmaka ostao je ispod implementacijskog praga od 80 ms. "
        "Korisnička studija stem-playera (IP5) ostaje za naknadno provođenje."
    )
    summary_result_en = (
        f"The evaluation used a catalogue of {dataset['catalog_tracks']} tracks, with {dataset['analyzed_tracks']} "
        f"({dataset['analysis_coverage']:.1%}) having an active audio analysis. HDBSCAN with min_cluster_size=5 "
        f"produced three clusters, {selected['noise_ratio']:.1%} noise and a silhouette score of "
        f"{selected['silhouette_cosine']:.3f}. The rolling-origin recommendation case study contained ten episodes "
        f"from one user. The hybrid obtained NDCG@10={hybrid['ndcg_at_10']:.3f}, Recall@10={hybrid['recall_at_10']:.3f} "
        f"and {hybrid['catalog_coverage_at_10']:.1%} catalogue coverage; it did not outperform CONTENT-PROFILE on relevance, "
        f"which reached NDCG@10={content_profile['ndcg_at_10']:.3f}. For {cold['analyzed_items_without_interactions_and_streams']} "
        f"analysed cold-start tracks, content-based radio returned an average of {cold['same_cluster_at_10']:.1%} tracks "
        f"from the same cluster. Of {operational['stem_jobs']} Demucs jobs, {operational['stem_ready']} completed successfully, "
        f"and all {storage['unique_stem_keys']} stem objects were reachable. Browser measurements kept P95 drift below the "
        "implemented 80 ms threshold. The IP5 stem-player user study remains to be conducted."
    )

    replacements = [
        {
            "old_starts_with": "Rad dokumentira arhitekturu, metode i implementacijske odluke te definira ponovljiv evaluacijski protokol",
            "new": summary_result_hr,
        },
        {
            "old_starts_with": "The thesis documents the architecture, methods and implementation decisions and defines a reproducible evaluation protocol",
            "new": summary_result_en,
        },
        {
            "old_starts_with": "Nakon uvoda, drugo poglavlje daje teorijski okvir",
            "new": "Nakon uvoda, drugo poglavlje daje teorijski okvir sustava preporuka, implicitnih povratnih informacija, audio-ugradnji, grupiranja, objašnjivosti i odvajanja izvora. Treće poglavlje opisuje korisnike, zahtjeve i arhitekturu platforme 808Music. Četvrto poglavlje definira istraživačku metodologiju, podatke, osnovne pristupe i metrike. Peto poglavlje detaljno prikazuje praktičnu implementaciju. Šesto poglavlje prikazuje provedena tehnička i izvanmrežna mjerenja; samo je korisnička studija IP5 ostavljena za naknadno provođenje. Sedmo poglavlje raspravlja nalaze, ograničenja i prijetnje valjanosti, a osmo donosi zaključak i smjernice za budući razvoj.",
        },
        {
            "old_starts_with": "Ovo poglavlje odvaja provedene tehničke provjere od planiranih istraživačkih mjerenja.",
            "new": (
                "U ovom su poglavlju prikazana stvarna mjerenja provedena 7. kolovoza 2026. nad zamrznutim lokalnim katalogom i pokrenutom aplikacijom. "
                "Obuhvaćeni su automatizirani testovi, proširivost klasifikacijskog podsustava, usporedba klasteriranja, rolling-origin evaluacija preporuka, "
                "cold-start i ablacijska analiza, live provjera API-ja i objektne pohrane, kontrolirani CPU/GPU Demucs benchmark te sinkronizacija četiriju stem tokova u Headless Chromeu. "
                "Zbog toga što baza sadrži interakcije samo jednoga aktivnog korisnika, rezultati preporuka predstavljaju eksplorativnu studiju slučaja, a ne populacijsku procjenu. "
                "Referentni izolirani stemovi nisu dostupni pa SDR nije izračunat. IP5, korisničko ispitivanje stem-playera, jedini je dio koji autor treba naknadno provesti."
            ),
        },
        {
            "old_starts_with": "Backend solution izgrađen je naredbom dotnet test",
            "new": (
                "Backend testovi pokrenuti su u Release konfiguraciji kako bi se izbjeglo zaključavanje DLL datoteka od pokrenutoga Debug API-ja; svih 36 testova prošlo je. "
                "Python ML servis prošao je svih 11 unittest provjera, a Angular/Karma svih 13 testova u Headless Chromeu. Produkcijski Angular build također je uspješno generiran. "
                "Live smoke-test dodatno je provjerio autentifikaciju, home i radio preporuke, playback manifest, stem manifest i dohvat potpisanih medijskih resursa."
            ),
        },
        {
            "old_starts_with": "Prolazak ovih provjera potvrđuje",
            "new": (
                "Prolazak automatiziranih i live provjera potvrđuje funkcionalnost obuhvaćenih ugovora na korištenom okruženju, ali sam po sebi ne dokazuje algoritamsku kvalitetu ni uporabljivost. "
                "Live API je bez tokena vratio 401, a s valjanim tokenom 20 home i 20 radio preporuka; razlog i sourceSignals bili su prisutni u svih 40 rezultata. "
                "Backend build je i dalje prijavio nullable upozorenja naslijeđenoga koda i poznate ranjivosti pojedinih verzija ovisnosti, što ostaje obvezan zadatak prije produkcijskog raspoređivanja."
            ),
        },
        {
            "old_starts_with": "Za svako izvođenje treba pohraniti točne parametre",
            "new": (
                f"Uspoređeno je devet konfiguracija nad {dataset['analyzed_tracks']} L2-normaliziranih 1280-dimenzionalnih ugradnji. "
                "Za K-means i aglomerativno grupiranje ispitani su K=8, 12 i 16, a za HDBSCAN minimalne veličine klastera 5, 10 i 15. "
                "Silueta je računata kosinusnom metrikom; HDBSCAN metrika isključuje šum. Stabilnost je procijenjena ARI-jem kroz više inicijalizacija ili male perturbacije ulaza, "
                "a semantička koherentnost udjelom pet najjačih oznaka pjesme koje se pojavljuju među deset agregirano najjačih oznaka klastera."
            ),
        },
        {
            "old_starts_with": "Predložak odluke:",
            "new": (
                f"Za istraživački prikaz odabran je HDBSCAN s min_cluster_size=5. Dao je tri klastera, {pct(selected['noise_ratio'])} šuma, "
                f"siluetu {fmt(selected['silhouette_cosine'])}, stabilnost ARI {fmt(selected['stability_ari'])} i semantičku koherentnost {fmt(selected['tag_coherence_at_10'])}. "
                "HDBSCAN s većim minimalnim klasterom imao je nešto višu siluetu, ali i približno dvostruko više šuma. Odabrano rješenje zato predstavlja kompromis odvojenosti, stabilnosti, koherentnosti i obuhvata; klaster ostaje pomoćni signal, a ne žanrovska istina."
            ),
        },
        {
            "old_starts_with": "Rezultati se prikazuju na istoj vremenskoj podjeli",
            "new": (
                f"Rolling-origin evaluacija koristi svih {dataset['interactions']} zabilježenih interakcija od 12. do 27. srpnja 2026. bez korištenja budućih događaja pri izgradnji profila. "
                "Pozitivni cilj je Liked, AddedToPlaylist ili PlayCompleted s omjerom dovršenosti najmanje 0,9. Za dovršenu reprodukciju presjek je vraćen na pripadajući PlayStarted kako trenutna pjesma ne bi procurila u recentTrackIds. "
                f"Nakon zahtjeva za najmanje deset ranijih događaja ostalo je {dataset['evaluation_episodes']} epizoda jednoga korisnika. Zbog tako malog uzorka nisu izračunati populacijski intervali pouzdanosti."
            ),
        },
        {
            "old_starts_with": "Interpretacija treba usporediti razlike na razini istih korisnika.",
            "new": (
                f"Hibrid nije nadmašio jednostavnije pristupe relevantnosti: NDCG@10={fmt(hybrid['ndcg_at_10'])} i Recall@10={fmt(hybrid['recall_at_10'])}, "
                f"dok su CONTENT-PROFILE i CONTENT-SEED ostvarili NDCG@10={fmt(content_profile['ndcg_at_10'])} i {fmt(content_seed['ndcg_at_10'])}. "
                f"Hibridna pokrivenost {pct(hybrid['catalog_coverage_at_10'])} bila je viša od CONTENT-PROFILE pokrivenosti {pct(content_profile['catalog_coverage_at_10'])}, "
                "ali niža od CONTENT-SEED pokrivenosti. Ovaj nalaz ne odbacuje hibridni dizajn, nego pokazuje da ručno zadane težine nisu potvrđene na raspoloživim podacima i da ih prije tvrdnje o poboljšanju treba podesiti i evaluirati na većem broju korisnika."
            ),
        },
        {
            "old_starts_with": "Cold-start korisnici i cold-start pjesme izdvajaju se",
            "new": (
                f"U katalogu je pronađeno {cold['analyzed_items_without_interactions_and_streams']} pjesama s aktivnom audio-analizom, bez interakcija i s nula streamova. "
                f"U {cold['seed_cases']} kontroliranih radio-slučajeva svaki je seed dobio deset rezultata; prosječno {pct(cold['same_cluster_at_10'])} bilo je iz istoga aktivnog klastera, "
                f"prosječni Jaccard pet najjačih oznaka iznosio je {fmt(cold['tag_jaccard_at_10'])}, a kosinusna sličnost {fmt(cold['embedding_similarity_at_10'])}. "
                f"U bazi postoji {dataset['cold_start_users_without_interactions']} registriranih korisnika bez interakcija. Za njih sustav tehnički vraća popularno-svježi fallback ili radio prema početnoj pjesmi, ali bez korisničkih ishoda nije moguće mjeriti osobnu relevantnost."
            ),
        },
        {
            "old_starts_with": "Objašnjenja su uključena radi transparentnosti",
            "new": (
                "Objašnjenja su funkcionalno provjerena kroz live v2 API: svih 20 home i 20 radio preporuka sadržavalo je reason i sourceSignals, dok su podudarne oznake bile prisutne kada je odgovarajući signal postojao. "
                "Primjeri razloga dosljedno su se odnosili na profil ili sličnost s početnom pjesmom, a vrijednosti seedSimilarity, sharedTags, clusterMatch i drugih komponenti ostale su dostupne za razvojni trag. "
                "Utjecaj objašnjenja na povjerenje i zadovoljstvo nije empirijski evaluiran, pa se iz funkcionalne potpunosti ne izvodi HCI zaključak."
            ),
        },
        {
            "old_starts_with": "Rezultati kvalitete trebaju biti navedeni po izvoru i po pjesmi",
            "new": (
                f"Referentni studijski stemovi nisu dostupni, pa SDR, SIR i SAR nisu izračunati i Tablica 11 to izričito označava. Operativna analiza obuhvatila je {operational['stem_jobs']} povijesnih poslova: "
                f"{operational['stem_ready']} je bilo Ready, {operational['stem_failed']} Failed, što daje {pct(operational['stem_success_rate_terminal'])} uspješnosti terminalnih poslova. "
                f"Svih {operational['stem_ready']} spremnih setova sadržavalo je točno četiri zapisa, svih {storage['unique_stem_keys']} stem objekata bilo je dohvatljivo i njihove pohranjene veličine podudarale su se s objektima. "
                f"Jedan od {storage['unique_master_keys']} master ključeva bio je nedostupan i pripadao je naslijeđenom zapisu bez aktivne analize; to je evidentiran podatkovni nedostatak, a ne uspješan slučaj."
            ),
        },
        {
            "old_starts_with": "Za svaki preglednik i scenarij bilježi se raspodjela apsolutnog odmaka.",
            "new": (
                "Četiri potpisana stem toka pjesme #17 reproducirana su u Headless Chromeu 151 na Windowsu 10, uz uzorkovanje odmaka svakih 50 ms i isti korekcijski prag od 80 ms kao u frontend implementaciji. "
                "Kontinuirana reprodukcija, seek i nastavak te prekid/nastavak imali su P95 ispod 0,24 ms i maksimum ispod 2,1 ms. Pri emuliranoj mreži od 750 kbit/s i 150 ms latencije P95 je iznosio 48,35 ms, a maksimum 72,42 ms, i dalje ispod praga. "
                "U prirodnim scenarijima korekcija nije bila potrebna. Dodatno izazvani odmak od 250 ms ispravljen je jednom, čime je funkcionalno potvrđeno okidanje korekcije. Četvrti scenarij simulira prekid i nastavak, a ne potpuno OS pozadinsko prigušivanje."
            ),
        },
        {
            "old_starts_with": "Rješenje se ne proglašava boljim samo zato što se sve komponente mogu pokrenuti.",
            "new": (
                "Tehnička funkcionalnost IP1–IP4 potvrđena je automatiziranim, live i kontroliranim mjerenjima, ali algoritamski rezultat ne podržava tvrdnju da je trenutačni hibrid bolji po relevantnosti. "
                "Njegova vrijednost u ovom skupu je integracija više signala, objašnjivost i veća pokrivenost od CONTENT-PROFILE pristupa. Cold-start novih pjesama i tehnička pouzdanost stem toka potvrđeni su unutar navedenih granica. "
                "Kvaliteta odvajanja bez referentnih stemova i korisnička vrijednost stem-playera ne smiju se proglasiti dokazanima."
            ),
        },
        {
            "old_starts_with": "Nakon popunjavanja svih tablica završni odlomak ovog poglavlja treba sažeti",
            "new": f"IP1: {answers['IP1']} IP2: {answers['IP2']} IP3: {answers['IP3']} IP4: {answers['IP4']} IP5: korisnička studija nije provedena i ostaje za autora rada.",
        },
        {
            "old_starts_with": "Ponderirani hibrid čini pretpostavke eksplicitnima.",
            "new": (
                f"Ponderirani hibrid čini pretpostavke eksplicitnima, ali ih rezultati na malom skupu nisu potvrdili. Hibridni NDCG@10 od {fmt(hybrid['ndcg_at_10'])} bio je niži od svih glavnih sadržajnih osnovnih pristupa. "
                "Ablacija bez oznaka nije promijenila NDCG@10, dok je uklanjanje audio-ugradnje smanjilo NDCG za 0,005, što na deset epizoda nije dovoljno za zaključak o stvarnoj važnosti signala. "
                "Uklanjanje ograničenja raznolikosti povećalo je pokrivenost, ali smanjilo intra-list raznolikost. Te nalaze treba koristiti za usmjeravanje prikupljanja podataka i podešavanja težina, ne za statističku generalizaciju."
            ),
        },
        {
            "old_starts_with": "Zbog toga se klaster u rangiranju koristi s manjom težinom",
            "new": (
                f"Odabrani HDBSCAN rezultat pokazao je dobru internu odvojenost i stabilnost, ali ablacijska razlika bez klastera pri NDCG@10 bila je 0,000 na ovom uzorku. "
                "Klaster je stoga opravdano zadržati kao pomoć za cold-start radio, istraživanje kataloga i administrativni prikaz, dok njegov doprinos općem personaliziranom rangiranju ostaje nedokazan. "
                "Automatski naziv iz oznaka nije nova nepogrešiva žanrovska istina."
            ),
        },
        {
            "old_starts_with": "Novost vrijednosti 0,2 za nedavno slušanu pjesmu snažno je kažnjava",
            "new": (
                "Novost vrijednosti 0,2 snažno kažnjava nedavno slušanu pjesmu. Uklanjanje tog faktora nije promijenilo Recall@10 ni NDCG@10 na deset epizoda, ali je znatno povećalo MRR jer je neke ciljeve pomaknulo naviše izvan prvih deset. "
                "To sugerira da dvostruka primjena novosti može biti preoštra, no uzorak je premalen za konačnu promjenu produkcijske konfiguracije. Faznu relaksaciju i učestalost ponovnog slušanja treba pratiti na većem skupu."
            ),
        },
        {
            "old_starts_with": "Trenutni pristup favorizira streaming i postupno učitavanje. Mjerenje mora pokazati",
            "new": (
                "Mjerenje u Headless Chromeu pokazalo je da je pri kontinuiranoj reprodukciji, seeku i prekidu/nastavku P95 odmaka ostao ispod 0,24 ms, a pri ograničenoj mreži 48,35 ms. Maksimum od 72,42 ms ostao je ispod praga od 80 ms bez korekcija; kontrolirani odmak od 250 ms izazvao je jednu korekciju. "
                "Rezultat podržava tehničku dostatnost pristupa u testiranom pregledniku i kratkom uzorku, ali ne jamči faznu preciznost, drugačije mobilne preglednike ni nečujnost korekcija."
            ),
        },
        {
            "old_exact": "7.13. Što se može zaključiti prije završnih mjerenja",
            "new": "7.13. Zaključci nakon tehničke evaluacije",
        },
        {
            "old_starts_with": "Iz implementacije i provedenih tehničkih testova može se zaključiti",
            "new": (
                "Tehnička evaluacija potvrđuje povezanu i operativno pouzdanu implementaciju, ali istodobno pokazuje da složeniji hibrid nije automatski relevantniji. Jednostavni CONTENT-PROFILE i CONTENT-SEED pristupi imali su viši NDCG@10, dok je hibrid povećao pokrivenost u odnosu na CONTENT-PROFILE i zadržao umjerenu raznolikost. "
                "Cold-start radio je bez interakcija stvarao semantički i vektorski slična susjedstva. HDBSCAN je dao stabilnu strukturu s kontroliranim šumom. Stem-podsustav je imao visoku povijesnu uspješnost, potpune objekte i odmak ispod praga u testiranom browseru."
            ),
        },
        {
            "old_starts_with": "Ova granica nije nedostatak dokumentiranja",
            "new": (
                "Zaključci ostaju ograničeni jednim korisnikom, deset preporučivačkih epizoda, jednim preglednikom i dvjema kratkim pjesmama u CPU/GPU benchmarku. Bez referentnih stemova nije procijenjena perceptualna kvaliteta separacije, a bez IP5 nije procijenjen doživljaj kontrole i zadovoljstvo korisnika. "
                "Neuspjeh hibrida da nadmaši osnovne pristupe valjan je rezultat i važan argument za veći skup, unaprijed registrirane metrike i učenje ili sustavno podešavanje težina."
            ),
        },
        {
            "old_starts_with": "[ZAVRŠNI ISTRAŽIVAČKI ZAKLJUČAK:",
            "new": (
                f"Provedena evaluacija pokazala je da HDBSCAN može organizirati audiovektore u stabilne i semantički koherentne skupine uz {pct(selected['noise_ratio'])} šuma, ali da trenutačni hibridni rezultat na ograničenoj studiji slučaja nije poboljšao relevantnost u odnosu na sadržajne osnovne pristupe. "
                f"Hibrid je ostvario NDCG@10={fmt(hybrid['ndcg_at_10'])}, Recall@10={fmt(hybrid['recall_at_10'])} i pokrivenost {pct(hybrid['catalog_coverage_at_10'])}, pa njegov doprinos treba promatrati kroz integraciju, transparentnost i obuhvat, ne kao dokaz superiornosti. "
                f"Sadržajni signali omogućili su rad s {cold['analyzed_items_without_interactions_and_streams']} cold-start pjesama bez interakcija. Stem tok je operativno bio pouzdan, GPU je u kontroliranom uzorku bio brži od CPU-a, a browser P95 odmaka ostao je ispod 80 ms. "
                "Prediktivna kvaliteta buduće klasifikacijske glave i HCI učinak objašnjenja nisu tvrđeni bez podataka. Konačni odgovor na IP5 mora se dodati nakon korisničke studije stem-playera."
            ),
        },
        {
            "old_starts_with": "• experiment_id:",
            "new": "• experiment_id: thesis-eval-20260807-v1",
        },
        {
            "old_starts_with": "• datum i vrijeme:",
            "new": "• datum i vrijeme: 7. kolovoza 2026.; live provjere 17:32–17:52 UTC, izvanmrežna analiza istoga dana",
        },
        {
            "old_starts_with": "• git commit:",
            "new": "• git commit: cd5cae3053fd1d8bd803c905e28570ce0fea0f93; direktorij experiments/ u trenutku mjerenja nije bio commitan",
        },
        {
            "old_starts_with": "• dataset_manifest_hash:",
            "new": "• dataset_manifest_hash: SHA-256 10d815d810138e5ec433c8000f08dd0b556eb62bd055466d9ec3eff2a31b4bd8 nad sortiranim nazivima i sadržajem izvezenih CSV datoteka",
        },
        {
            "old_starts_with": "• embedding_model_hash:",
            "new": "• embedding_model_hash: binarni hash nije izložen u runtime-okruženju; spremljeni identifikator je discogs-effnet-bs64-1, Essentia model mtg-jamendo-discogs-effnet, verzija 1",
        },
        {
            "old_starts_with": "• model ili algoritam:",
            "new": "• model ili algoritam: K-means, aglomerativno grupiranje, HDBSCAN, preporučivačke osnovne i hibridne varijante te Demucs htdemucs",
        },
        {
            "old_starts_with": "• parametri:",
            "new": "• parametri: zapisani u evaluate.py, benchmark_demucs.py i measure_stem_sync.mjs te u CSV/JSON rezultatima",
        },
        {
            "old_starts_with": "• random_seed:",
            "new": "• random_seed: 42 za glavno K-means/PCA izvođenje; 7, 42, 808, 2026 i 7777 za stabilnost; 808 za perturbacije",
        },
        {
            "old_starts_with": "• hardver i operacijski sustav:",
            "new": "• hardver i operacijski sustav: Windows x64, CPU x86-64, NVIDIA GeForce RTX 3060 Ti, Headless Chrome 151, PyTorch 2.6.0+cu124",
        },
        {
            "old_starts_with": "• ulazni broj stavki i korisnika:",
            "new": "• ulazni broj stavki i korisnika: 181 pjesma, 178 aktivnih analiza, 15 registriranih korisnika, 1 korisnik s interakcijama i 584 interakcije",
        },
        {
            "old_starts_with": "• izlazna datoteka metrika:",
            "new": "• izlazna datoteka metrika: experiments/thesis_evaluation/results/final_results.json i pridružene CSV/JSON datoteke",
        },
        {
            "old_starts_with": "• napomena o odstupanju od protokola:",
            "new": "• napomena o odstupanju od protokola: samo jedan korisnik i deset epizoda za preporuke; bez referentnih stemova; po dvije pjesme u CPU/GPU skupini; IP5 nije proveden",
        },
        {
            "old_starts_with": "3. Provesti eksperimentalni protokol i zamijeniti sva polja [UNIJETI]",
            "new": "3. Provesti IP5 i zamijeniti njegova preostala polja [UNIJETI] stvarnim podacima; ne unositi procijenjene rezultate.",
        },
    ]

    payload = {
        "paragraph_replacements": replacements,
        "delete_paragraphs": [
            {"old_starts_with": "VAŽNO ZA ZAVRŠNU VERZIJU:"},
            {"old_starts_with": "DOPUNITI PRIJE PREDAJE: u sljedeći odlomak"},
        ],
        "delete_all_exact_except_first": ["NAPOMENA ZA DOPUNU"],
        "tables": [
            {
                "caption": "Tablica 6. Rezultati dostupnih automatiziranih tehničkih provjera",
                "rows": [
                    ["Komponenta", "Naredba ili postupak", "Rezultat"],
                    [".NET backend", "dotnet test -c Release", "36 prošlo, 0 palo, 0 preskočeno"],
                    ["Python ML servis", "python -m unittest discover -s tests -v", "11 prošlo, 0 palo"],
                    ["Angular frontend", "npm test -- --watch=false; npm run build", "13 testova prošlo; produkcijski build uspješan"],
                ],
            },
            {"caption": "Tablica 7. Usporedba algoritama grupiranja audio-ugradnji", "rows": cluster_rows},
            {
                "caption": "Tablica 8. Opis skupa za evaluaciju sustava preporuka",
                "rows": [
                    ["Svojstvo", "Vrijednost"],
                    ["Razdoblje trening interakcija", "Rolling-origin: svi događaji prije pojedinog cilja, 12.–27. 7. 2026."],
                    ["Razdoblje testnih interakcija", "12.–27. 7. 2026.; 10 valjanih epizoda"],
                    ["Broj korisnika u evaluaciji", "1"],
                    ["Broj cold-start korisnika", f"{dataset['cold_start_users_without_interactions']} bez interakcija; nisu u metriki relevantnosti"],
                    ["Broj pjesama dostupnih pri presjeku", f"{dataset['analyzed_tracks']} analiziranih od {dataset['catalog_tracks']}"],
                    ["Broj pozitivnih testnih događaja", f"{dataset['evaluation_episodes']} nakon filtra; 12 prije filtra povijesti"],
                    ["Udio pjesama sa spremnom audio-analizom", pct(dataset["analysis_coverage"])],
                ],
            },
            {"caption": "Tablica 9. Glavne metrike rangiranja pri K = 10", "rows": recommendation_rows},
            {"caption": "Tablica 10. Analiza uklanjanja signala iz hibridnog modela", "rows": ablation_rows},
            {
                "caption": "Tablica 11. Rezultati kvalitete automatskog odvajanja na referentnom skupu",
                "rows": [
                    ["Izvor", "Medijan SDR", "Sredina SDR", "Interkvartilni raspon", "Broj referentnih pjesama"],
                    ["Vocals", "N/P", "N/P", "N/P", "0"],
                    ["Drums", "N/P", "N/P", "N/P", "0"],
                    ["Bass", "N/P", "N/P", "N/P", "0"],
                    ["Other", "N/P", "N/P", "N/P", "0"],
                ],
            },
            {"caption": "Tablica 12. CPU/GPU izvedba separacijskog toka", "rows": demucs_rows},
            {"caption": "Tablica 13. Mjerenje sinkronizacije stemova u pregledniku", "rows": sync_rows},
            {
                "caption": "Prilog B. Kontrolna lista prije pokretanja eksperimenata",
                "word_index": 37,
                "rows": [
                    ["Provjera", "Status", "Dokaz ili lokacija"],
                    ["Licenca ili dopuštenje za svaki zvučni zapis", "AUTOR MORA POTVRDITI", "Licencni podaci nisu dostupni u izvezenoj bazi"],
                    ["Jedinstven track_id i obvezan artist_id", "DA", "catalog.csv i artist_tracks.csv; 181 pjesma"],
                    ["Vremenska granica preporuka dokumentirana", "DA", "Poglavlja 4.2 i 6.5; rolling-origin presjek"],
                    ["Kandidati dostupni samo prije trenutka predviđanja", "DA", "evaluate.py; presjek se primjenjuje prije izgradnje profila"],
                    ["Isti korisnici i kandidati za sve usporedbe", "DA", "recommendation_episodes.csv; 10 zajedničkih epizoda"],
                    ["Hardver i verzija Demucsa zapisani", "DA", "demucs_benchmark.json; htdemucs, PyTorch 2.6.0+cu124"],
                    ["Preglednici i mrežni profili zapisani", "DA", "stem_sync_browser.json; Headless Chrome 151"],
                    ["Informirani pristanak i plan privatnosti odobreni", "IP5 – NIJE PROVEDENO", "Prilog A; potrebno prije korisničke studije"],
                ],
            },
        ],
        "figures": [
            {
                "instruction_starts_with": "Umetnuti snimku zaslona preporučene pjesme.",
                "path": str((FIGURES / "explanation_card.png").resolve()),
            },
            {
                "instruction_starts_with": "Umetnuti UMAP ili t-SNE prikaz samo kao vizualnu pomoć.",
                "path": str((FIGURES / "selected_clusters_pca.png").resolve()),
            },
            {
                "instruction_starts_with": "Umetnuti heatmap prosječnih najjačih oznaka po klasteru.",
                "path": str((FIGURES / "cluster_semantic_profile.png").resolve()),
            },
            {
                "instruction_starts_with": "Umetnuti Pareto ili bubble graf.",
                "path": str((FIGURES / "recommendation_comparison.png").resolve()),
            },
            {
                "instruction_starts_with": "Umetnuti graf NDCG@10 ili Recall@10 po skupinama",
                "path": str((FIGURES / "history_quality.png").resolve()),
            },
            {
                "instruction_starts_with": "Umetnuti scatter graf sa zasebnim bojama za CPU/GPU",
                "path": str((FIGURES / "demucs_cpu_gpu.png").resolve()),
            },
        ],
    }

    (RESULTS / "thesis_document_data.json").write_text(
        json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(RESULTS / "thesis_document_data.json")


if __name__ == "__main__":
    main()
