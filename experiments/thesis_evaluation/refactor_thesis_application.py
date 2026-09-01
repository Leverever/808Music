from __future__ import annotations

from copy import deepcopy
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

from lxml import etree


SOURCE = Path(r"C:\808Music\artifacts\Prijedlog_teme_zavrsnog_rada_808Music_stem_separation.docx")
OUTPUT = Path(r"C:\808Music\artifacts\Prijedlog_teme_zavrsnog_rada_808Music_refaktorirana.docx")
W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
XML = "http://www.w3.org/XML/1998/namespace"
NS = {"w": W}


def paragraph_text(paragraph) -> str:
    return "".join(paragraph.xpath(".//w:t/text()", namespaces=NS)).strip()


def replace_paragraph_text(paragraph, value: str) -> None:
    text_nodes = paragraph.xpath(".//w:t", namespaces=NS)
    if not text_nodes:
        run = etree.Element(f"{{{W}}}r")
        text_node = etree.SubElement(run, f"{{{W}}}t")
        paragraph.append(run)
        text_nodes = [text_node]
    text_nodes[0].text = value
    if value[:1].isspace() or value[-1:].isspace():
        text_nodes[0].set(f"{{{XML}}}space", "preserve")
    for node in text_nodes[1:]:
        node.text = ""


REPLACEMENTS = {
    "Predloženi završni rad bavi se primjenom metoda mašinskog učenja za personalizaciju slušanja i otkrivanja glazbe u okviru platforme 808Music. Rad obuhvaća dva povezana aspekta korisničkog iskustva: personalizaciju odabira glazbe pomoću objašnjivog hibridnog sustava preporuke te personalizaciju samog načina slušanja pomoću automatskog odvajanja i interaktivnog miksanja zvučnih izvora (engl. stem separation). Temeljni problem čine ograničena relevantnost općih preporuka, problem „cold“ starta kod novih korisnika i novog sadržaja, nedovoljna transparentnost algoritamskih preporuka te ograničena kontrola slušatelja nad zvučnim slojevima glazbe.":
        "Predloženi završni rad bavi se primjenom metoda strojnog učenja za personalizaciju iskustva otkrivanja i slušanja glazbe u platformi 808Music. Rad povezuje dva aspekta: personalizaciju odabira sadržaja objašnjivim hibridnim sustavom preporuka i personalizaciju načina reprodukcije automatskim odvajanjem te interaktivnim miješanjem zvučnih izvora (engl. music source separation). Problem predstavljaju rijetke korisničke interakcije, cold-start novih korisnika i pjesama, ograničena transparentnost algoritamskog rangiranja te mala kontrola slušatelja nad pojedinim zvučnim slojevima gotove snimke.",
    "U radu će se razviti, integrirati i evaluirati objašnjivi hibridni sustav za preporuku koji povezuje značajke dobivene analizom zvuka, a to su: vektorski prikazi zvučnog sadržaja (engl. embeddings), oznake žanra, raspoloženja i teme te pripadnost zvučnim klasterima sa implicitnim povratnim informacijama od korisnika. Rangiranje će uzeti u obzir i kontekst preporuke, sličnost s početnim sadržajem, svježinu, popularnost, starost sadržaja te raznolikost izvođača i izdanja.":
        "Sustav će povezati audio-ugradnje dobivene prethodno treniranim modelom Discogs-EffNet, semantičke oznake i pripadnost zvučnim klasterima s vremenski ponderiranim implicitnim interakcijama korisnika. Profil ukusa gradit će se iz započetih i dovršenih reprodukcija, preskakanja, oznaka sviđanja i radnji nad popisima za reprodukciju. Rangiranje će se prilagođavati namjeri zahtjeva, odnosno početnoj stranici, radiju temeljenom na pjesmi, automatskom nastavku i dnevnom tematskom popisu, uz signale svježine, popularnosti, novosti i raznolikosti. Uz preporuku će se prikazivati razlog izveden iz stvarno korištenih signala, bez tvrdnje o poboljšanju korisničkog iskustva koja nije empirijski ispitana.",
    "Glavne komponente rada bit će „multi-label“ klasifikator modernih glazbenih žanrova razvijen prijenosom učenja, metode grupiranja zvučnih zapisa, profil korisničkog ukusa, hibridni sustav za preporuku, objašnjenja preporuka i evaluacijski okvir. Drugi praktični podsustav obuhvatit će asinkrono odvajanje vokala, bubnjeva, basa i ostalih izvora određenim ML modelom, pohranu izdvojenih izvora te njihovu sinkroniziranu i korisnički upravljivu reprodukciju u web-playeru. Sustav je namijenjen slušateljima koji žele relevantnije otkrivanje glazbe i veću kontrolu nad načinom slušanja, izvođačima koji objavljuju novu glazbu bez prethodne povijesti interakcija te administratorima koji upravljaju tematskim popisima i ML procesima.":
        "Klasifikacijski dio rada neće zahtijevati treniranje nove produkcijske glave za suvremene žanrove, nego implementaciju i funkcionalnu provjeru proširive infrastrukture kojom se dodatna višeznačna klasifikacijska glava može naknadno registrirati, trenirati i učitati kada postoji odgovarajući označeni skup. Nenadzirano grupiranje poslužit će za organizaciju kataloga i kao pomoćni signal preporuka. Drugi praktični tok obuhvatit će asinkrono odvajanje vokala, bubnjeva, basa i ostalih izvora modelom Demucs, njihovu pohranu te sinkronizirano miješanje u web-playeru. Rješenje je namijenjeno slušateljima, izvođačima, administratorima te razvojnom i istraživačkom timu platforme.",

    "Svrha završnog rada jest istražiti kako se metode strojnog učenja mogu primijeniti na poboljšanje korisničkog iskustva na platformama za streaming glazbe, posebno gledano iz dva aspekta: pronalazak i odabir sadržaja te način njegove reprodukcije. Automatsko razumijevanje zvuka i implicitno ponašanje korisnika povezat će se radi poboljšanja relevantnosti, raznolikosti i novosti preporuka, dok će odvajanje zvučnih izvora omogućiti korisniku aktivnu kontrolu nad vokalima, bubnjevima, basom i ostalim slojevima iz izvorne datoteke. Posebna se pozornost posvećuje „cold“ startu, transparentnosti preporuka te utjecaju objašnjenja i interaktivne kontrole zvuka na povjerenje, angažman i spremnost korisnika na otkrivanje nepoznate glazbe.":
        "Svrha završnog rada jest istražiti kako povezivanje razumijevanja zvučnog sadržaja i implicitnog ponašanja korisnika može podržati personalizirano otkrivanje glazbe te kako odvajanje izvora može proširiti personalizaciju na sam način slušanja. Vrijednost rješenja promatrat će se kroz relevantnost i pokrivenost preporuka, cold-start podršku, raznolikost, transparentnost, operativnu pouzdanost ML tokova i tehničku sinkronizaciju izdvojenih izvora. Objašnjenja preporuka razmatrat će se kao mehanizam transparentnosti koji bi prema literaturi mogao povećati razumljivost i povjerenje, ali se taj HCI učinak neće tvrditi bez zasebnog korisničkog ispitivanja.",
    "Cilj završnog rada jest razviti i evaluirati cjelovit ML okvir za personalizirano otkrivanje i slušanje glazbe unutar platforme. Okvir će objediniti objašnjivi hibridni sustav preporuke, audio-značajke dobivene modelima strojnog učenja, prijenos učenja za suvremene žanrove, nenadzirano grupiranje skladbi i korisničke interakcije. Uz to će se integrirati i analizirati podsustav za odvajanje zvučnih izvora i sinkronizirani stem-player. Preporučivački rezultati usporedit će se s popularnim, sadržajnim i ponašajnim baznim pristupima, dok će se tehnička pouzdanost i korisnička vrijednost odvajanja izvora provjeriti funkcionalnim mjerenjima i korisničkom evaluacijom.":
        "Cilj završnog rada jest implementirati, dokumentirati i evaluirati cjelovit prototip unutar platforme 808Music: audio-analizu, proširivu infrastrukturu klasifikacijskih glava, grupiranje pjesama, korisnički profil, hibridno rangiranje, cold-start načine rada, objašnjenja preporuka, dnevne personalizirane popise te asinkroni Demucsov tok i stem-player. Sustav preporuka usporedit će se s popularnim i sadržajnim osnovnim pristupima vremenskom rolling-origin evaluacijom i analizom uklanjanja signala. Ako je povijest interakcija ograničena, nalazi će se tumačiti kao eksplorativna studija slučaja, a ne kao dokaz kvalitete za populaciju. Stem-podsustav provjerit će se funkcionalno i izvedbeno, dok će korisnička vrijednost stem-playera biti predmet zasebne studije IP5.",
    "• unaprijediti opis novih skladbi korištenjem audio-ugradnji, automatskih oznaka i klasifikacije modernih glazbenih žanrova;":
        "• funkcionalno provjeriti tok audio-analize i mogućnost dodavanja, treniranja i učitavanja nove klasifikacijske glave bez obveze izgradnje konkretnog modela suvremenih žanrova;",
    "• ispitati prikladnost algoritama K-means, aglomerativnog grupiranja i HDBSCAN-a za organizaciju glazbenog kataloga;":
        "• usporediti K-means, aglomerativno grupiranje i HDBSCAN prema kvaliteti, stabilnosti, šumu i semantičkoj koherentnosti klastera;",
    "• izgraditi profil korisničkog ukusa iz reprodukcija, dovršenih slušanja, preskakanja, oznaka sviđanja i radnji nad popisima za reprodukciju;":
        "• izgraditi vremenski ponderirani profil korisnika te podržati personalizirane i cold-start preporuke za različite namjere slušanja;",
    "• usporediti hibridno rangiranje s jednostavnijim baznim pristupima i provesti analizu doprinosa pojedinih signala;":
        "• usporediti hibridno rangiranje s jednostavnijim osnovnim pristupima i provesti analizu uklanjanja audio-ugradnji, oznaka, klastera, vremenskog slabljenja, novosti i ograničenja raznolikosti;",
    "• prikazati razumljiva obrazloženja preporuka te procijeniti utjecaj objašnjenja i interaktivnog miksanja izdvojenih zvučnih izvora na korisničko iskustvo;":
        "• funkcionalno provjeriti sljedivost objašnjenja do signala rangiranja, bez izvođenja empirijskog zaključka o njihovu utjecaju na povjerenje ili zadovoljstvo;",
    "• dokumentirati arhitekturu, implementaciju, metodologiju evaluacije i ograničenja rješenja.":
        "• integrirati Demucs i sinkronizirani stem-player, provjeriti pouzdanost i izvedbu toka te pripremiti IP5 korisničku studiju osjećaja kontrole i zadovoljstva slušanjem.",

    "Slušatelji: koriste personaliziranu početnu stranicu, dnevne AI/ML generirane tematske popise za reprodukciju (engl. playlists), radio temeljen na pjesmi, automatski nastavak reprodukcije, objašnjenja preporuka i interaktivno upravljanje izdvojenim zvučnim izvorima.":
        "Slušatelji: koriste personaliziranu početnu stranicu, dnevne tematske popise, radio temeljen na pjesmi, automatski nastavak reprodukcije, objašnjenja preporuka i interaktivno upravljanje izdvojenim zvučnim izvorima.",
    "Izvođači: učitavaju i uređuju pjesme te koriste automatsku analizu zvuka kako bi novi sadržaj mogao biti uključen u preporuke i prije prikupljanja većeg broja interakcija.":
        "Izvođači: učitavaju i uređuju pjesme te pokreću audio-analizu kako bi se novi sadržaj mogao uključiti u sadržajne preporuke prije prikupljanja dovoljnog broja interakcija.",
    "Administratori: definiraju teme i pozitivne ili negativne oznake za automatski generirane popise, prate zakazane ML zadatke te ručno pokreću obradu kada je to potrebno.":
        "Administratori: definiraju teme i pripadajuće oznake za automatski generirane popise, prate statuse ML poslova te po potrebi pokreću analizu, grupiranje ili odvajanje izvora.",
    "Istraživač/razvojni tim: uspoređuje algoritme, prati metrike i analizira utjecaj pojedinih signala na preporuke.":
        "Istraživač i razvojni tim: uspoređuju algoritme, prate metrike, analiziraju doprinos pojedinih signala i provjeravaju ponovljivost eksperimenata.",
    "Sigurna autentifikacija i autorizacija korisnika, izvođača i administratora":
        "Sigurna autentifikacija i autorizacija slušatelja, izvođača i administratora",
    "Pouzdano učitavanje zvučnih datoteka i pohrana izvornog zapisa i izvedenih datoteka":
        "Pouzdano učitavanje i pohrana izvornoga zvuka, audio-analiza, generiranih popisa i izdvojenih stem datoteka",
    "Asinkrona obrada dugotrajnih ML zadataka bez blokiranja glavnog API-a":
        "Asinkrona i idempotentna obrada dugotrajnih ML poslova, sa statusima Pending, Processing, Ready i Failed, bez blokiranja API-ja v2",
    "Bilježenje implicitnih interakcija korisnika sa sadržajem i zaštita od višestrukog zapisa istog događaja":
        "Bilježenje vremenski označenih implicitnih interakcija i zaštita od višestrukog zapisa istoga događaja",
    "Prilagodba preporuka kontekstu: opće otkrivanje, radio, automatska reprodukcija i dnevni tematski popisi":
        "Prilagodba rangiranja namjeri: opće otkrivanje, radio prema početnoj pjesmi, automatski nastavak i dnevni tematski popisi",
    "Objašnjivost preporuka, kontrola raznolikosti te smanjenje pretjeranog ponavljanja nedavno slušanih pjesama":
        "Sljediva objašnjenja preporuka, kontrola raznolikosti i smanjivanje pretjeranog ponavljanja nedavno slušanih pjesama",
    "Responzivno web-sučelje prilagođeno desktop i mobilnim uređajima":
        "Sinkronizirana i korisnički upravljiva reprodukcija master zapisa ili više stem tokova u responzivnom web-sučelju",
    "Mogućnost ponovljivog testiranja modela, algoritama i konfiguracija":
        "Ponovljiva evaluacija uz dokumentiran vremenski presjek, konfiguracije, seedove, hardver, ulazne podatke i ograničenja",
    "Automatsko izdvajanje vektorskih audio-značajki te klasifikacija žanra, raspoloženja, teme i drugih semantičkih oznaka":
        "Automatsko izdvajanje 1280-dimenzionalnih audio-ugradnji te oznaka žanra, raspoloženja, teme i instrumentacije",
    "Prijenos učenja nad postojećim audio-ugradnjama radi prepoznavanja suvremenih i slabije zastupljenih žanrova":
        "Registracija, funkcionalna validacija i naknadno treniranje dodatnih klasifikacijskih glava nad postojećim audio-ugradnjama kada je dostupan označeni skup",
    "Grupiranje skladbi prema zvučnoj sličnosti i automatsko opisivanje klastera reprezentativnim oznakama":
        "Grupiranje pjesama prema audio-ugradnjama te opisivanje klastera reprezentativnim semantičkim oznakama",
    "izrada vremenski ponderiranog profila korisničkih preferencija iz pozitivnih i negativnih implicitnih povratnih informacija":
        "Izrada vremenski ponderiranog profila korisnika iz pozitivnih i negativnih implicitnih povratnih informacija",
    "Generiranje preporuka za početnu stranicu, radio na temelju pjesme, automatski nastavak reprodukcije i dnevne personalizirane popise za reprodukciju":
        "Generiranje preporuka za početnu stranicu, radio temeljen na pjesmi, automatski nastavak i dnevne personalizirane tematske popise",
    "Cold start temeljen na zvučnom sadržaju, temama, svježini i popularnosti kada podaci o preferencijama korisnika nisu dostupni":
        "Cold-start preporuke temeljene na zvučnom sadržaju, oznakama, klasterima, svježini i popularnosti kada nema dovoljne povijesti ponašanja",
    "Prikaz razloga preporuke i relevantnog konteksta":
        "Prikaz kratkog razloga, podudarnih oznaka i izvornih signala preporuke",
    "Administracija tema, oznaka i zakazanih ML procesa":
        "Generiranje dnevnih popisa te administracija tema, oznaka i zakazanih ML procesa",
    "Odvajanje vokala, bubnjeva, basa i ostalih izvora pomoću određenog modela te njihova sinkronizirana reprodukcija u web-playeru":
        "Odvajanje vokala, bubnjeva, basa i ostalih izvora modelom Demucs, dohvat potpisanih resursa te sinkronizirano miješanje u stem-playeru",
    "Odvajanje vokala, bubnjeva, basa i ostalih izvora pomoću određenog modela te njihova sinkronizirana reprodukcija u web-playeru":
        "Odvajanje vokala, bubnjeva, basa i ostalih izvora modelom Demucs, dohvat potpisanih resursa te sinkronizirano miješanje u stem-playeru",

    "Teorijski dio rada obuhvatit će sadržajno, kolaborativno i hibridno preporučivanje; implicitne povratne informacije i vremensko slabljenje utjecaja interakcija, cold start korisnika i sadržaja, vektorske prikaze zvuka i višeznačnu klasifikaciju žanrova, prijenos učenja, nenadzirano grupiranje, kontekstno rangiranje, novost i raznolikost preporuka, objašnjivu umjetnu inteligenciju te korisnički usmjerenu evaluaciju. Kao drugi ravnopravni tok personalizacije obradit će se odvajanje zvučnih izvora: osnovni pristupi, arhitektura, kompromis kvalitete i vremena CPU/GPU obrade, asinkrona obrada, sinkronizacija više zvučnih tokova te HCI aspekti korisničkog miksanja.":
        "Teorijski dio rada obuhvatit će sadržajne, kolaborativne i hibridne sustave preporuka; implicitne povratne informacije i vremensko slabljenje; cold-start korisnika i pjesama; audio-ugradnje, višeznačnu klasifikaciju i prijenosno učenje; nenadzirano grupiranje; kontekstno rangiranje; novost, pokrivenost i raznolikost; objašnjive preporuke te metodologiju izvanmrežne evaluacije. Drugi tok obuhvatit će odvajanje glazbenih izvora, Demucsovu arhitekturu, kompromis kvalitete i vremena CPU/GPU obrade, asinkrone radne procese, objektnu pohranu, sinkronizaciju više tokova u pregledniku te HCI aspekte stem-playera.",
    "Praktični dio obuhvatit će nadogradnju i dokumentiranje platforme 808Music, pripremu i validaciju skupa podataka za moderne žanrove, učenje i kalibraciju klasifikacijskog modela, usporedbu metoda grupiranja, izračun korisničkih profila, implementaciju i podešavanje hibridni preporuka, izradu objašnjenja te integraciju rezultata u web-sučelje. Paralelno će se realizirati cjelovit tok od zahtjeva za odvajanje izvora preko RabbitMQ radnika i modela za razdvajanje i sinkroniziranog upravljanja glasnoćom svakog izvora u web-playeru. Izradit će se evaluacijski okvir za ponovljivu usporedbu pristupa preporukama te provjeru kvalitete, pouzdanosti i uporabljivosti stem-separation podsustava.":
        "Praktični dio rada obuhvatit će nadogradnju i dokumentiranje API-ja v2 i web-aplikacije 808Music, audio-analizu prethodno treniranim modelima, funkcionalnu provjeru infrastrukture dodatnih klasifikacijskih glava, usporedbu grupiranja, izgradnju korisničkog profila, hibridno rangiranje, cold-start načine rada, objašnjenja i dnevne popise. Stem-tok obuhvatit će zahtjev iz klijenta, RabbitMQ red, zasebne CPU/GPU Demucs radnike, S3/MinIO pohranu, povratni status i sinkronizirano upravljanje glasnoćom izvora u web-playeru. Izradit će se ponovljiv evaluacijski okvir koji razdvaja algoritamsku relevantnost, operativnu pouzdanost, tehničku sinkronizaciju i korisničku vrijednost.",
    "Angular 18, TypeScript, RxJS i Angular Material za izradu odzivnog korisničkog sučelja, prikaz personaliziranih sadržaja i objašnjenja te mobilni audio-player":
        "Angular 18, TypeScript, RxJS i Angular Material – responzivno korisničko sučelje, personalizirani sadržaji, objašnjenja i stem-player",
    ".NET 10 i ASP.NET Core za verzionirani REST API, autentifikaciju, poslovnu logiku i integracija aplikacijskih servisa":
        ".NET 10 i ASP.NET Core – verzionirani REST API v2, autentifikacija, poslovna logika i integracija aplikacijskih servisa",
    "Entity Framework Core i relacijska baza podataka za pohranu kataloga, interakcija, profila, analiza, klastera i generiranih popisa;":
        "Entity Framework Core i relacijska baza – katalog, interakcije, profili, analize, klasteri, generirani popisi i statusi ML poslova",
    "Python za implementaciju radnih procesa za analizu zvuka, grupiranje i prijenos učenja":
        "Python – radni procesi audio-analize, grupiranja, odvajanja izvora i evaluacijske skripte",
    "Essentia i TensorFlow za učitavanje zvuka, Discogs-EffNet audio-ugradnje te klasifikacija žanra, raspoloženja i teme":
        "Essentia i TensorFlow – učitavanje zvuka, Discogs-EffNet audio-ugradnje i postojeće klasifikacijske glave oznaka",
    "scikit-learn za K-means, aglomerativno grupiranje, HDBSCAN, priprema podataka i izračun evaluacijskih metrika":
        "scikit-learn – K-means, aglomerativno grupiranje, HDBSCAN, priprema podataka i evaluacijske metrike",
    "Demucs za odvajanje vokala, bubnjeva, basa i ostalih zvučnih izvora":
        "Demucs i PyTorch – odvajanje vokala, bubnjeva, basa i ostalih zvučnih izvora na CPU-u ili GPU-u",
    "RabbitMQ za asinkrono slanje poslova analize zvuka, grupiranja i odvajanja izvora":
        "RabbitMQ – asinkrono slanje poslova audio-analize, grupiranja i odvajanja izvora",
    "S3-kompatibilna pohrana i MinIO za pohranu izvornog zvuka, izdvojenih izvora i izvedenih datoteka":
        "S3-kompatibilna pohrana i MinIO – master zapisi, stemovi i izvedene datoteke dostupne potpisanim URL-ovima",
    "Docker za ponovljivo pokretanje API-a, infrastrukture i CPU/GPU ML radnika":
        "Docker – ponovljivo pokretanje API-ja, infrastrukture te CPU/GPU ML radnika",
    "Git – upravljanje izvornim kodom i praćenje razvoja rješenja":
        "Git – upravljanje izvornim kodom, praćenje konfiguracija i dokumentiranje eksperimentalnih verzija",
    "Rješenje će se provjeriti kroz jedinične i integracijske testove backend, frontend i ML komponenti te kroz skup eksperimenata. Podaci će se dijeliti kronološki kako bi se izbjeglo korištenje budućih interakcija pri izradi korisničkog profila. Klasifikacija će se evaluirati metrikama F1, preciznost, odziv i srednja prosječna preciznost. Grupiranje će se usporediti pomoću koeficijenta siluete, Davies–Bouldinova indeksa, stabilnosti klastera i semantičke koherentnosti oznaka.":
        "Rješenje će se provjeriti jediničnim i integracijskim testovima backend, frontend i ML komponenti, produkcijskim buildom te live provjerom autentifikacije, API-ja v2, redova poruka, objektne pohrane i potpisanih medijskih resursa. Proširivost klasifikacijskog podsustava provjerit će se učitavanjem manifesta, dimenzijama izlaza, pragovima i odbijanjem neusklađenih metapodataka; F1, mAP i slične metrike neće se navoditi bez reprezentativnog označenog skupa i treniranog modela. K-means, aglomerativno grupiranje i HDBSCAN usporedit će se koeficijentom siluete, Davies–Bouldinovim indeksom, stabilnošću, šumom i semantičkom koherentnošću.",
    "Preporučivač će se usporediti s popularnim, sadržajnim i ponašajnim baznim pristupima pomoću metrika Precision@K, Recall@K, NDCG@K, MRR, pokrivenost kataloga, raznolikost, novost i zastupljenost manje popularnih izvođača. Podsustav odvajanja izvora provjerit će se mjerenjem uspješnosti poslova, vremena CPU/GPU obrade, potpunosti izlaznih izvora, ponašanja pri pogrešci i odstupanja sinkronizacije tijekom reprodukcije. Kvaliteta i korisnost miksanja procijenit će se slušanjem i korisničkim ocjenama. Ako broj sudionika i raspoloživo vrijeme to dopuste, provest će se manja anketa sa ispitanicima u kojoj će se usporediti nepersonalizirani popis, personalizirani popis bez objašnjenja i personalizirani popis s objašnjenjima, a dodatno će se ispitati doprinos stem-playera osjećaju kontrole, angažmanu i zadovoljstvu.":
        "Sustav preporuka evaluirat će se kronološkim rolling-origin presjecima bez curenja budućih interakcija. Popularni, svježe-popularni, sadržajni, oznaka/klaster i hibridni pristupi usporedit će se pomoću Precision@K, Recall@K, NDCG@K, MRR-a, pokrivenosti kataloga i raznolikosti, uz posebnu cold-start i ablacijske analize. Objašnjenja će se funkcionalno provjeriti u API odgovoru, bez korisničke usporedbe varijante s objašnjenjem i bez njega. Stem-tok provjerit će se stopom uspješnosti, potpunošću objekata, CPU/GPU vremenom, reakcijom na pogrešku i raspodjelom sinkronizacijskog odmaka u pregledniku. SDR/SIR/SAR izračunat će se samo ako postoje referentni izolirani stemovi. IP5 će korisničkim zadacima i upitnikom ispitati razumljivost kontrola, ostvarivanje željenog miksa, osjećaj kontrole, uočene artefakte i namjeru ponovne uporabe.",

    "Rad je izvediv jer postojeća platforma već sadrži osnovnu infrastrukturu za učitavanje glazbe, ML obradu, prikupljanje interakcija, generiranje preporuka i prikaz rezultata. Aktivnosti su stoga usmjerene na metodološku nadogradnju, usporedbu pristupa, evaluaciju i dokumentiranje.":
        "Rad je izvediv jer platforma 808Music već sadrži katalog, autentifikaciju, pohranu zvuka, API v2, prikupljanje interakcija, Python ML radnike, RabbitMQ, S3/MinIO i web-player. Aktivnosti su usmjerene na usklađivanje implementacije, zamrzavanje evaluacijskog skupa, usporedbu pristupa, tehnička mjerenja, korisničku studiju IP5 i završno dokumentiranje. Ograničen broj aktivnih korisnika neće se prikrivati, nego će izravno odrediti doseg zaključaka.",

    "Uvod: motivacija, problem, istraživačka pitanja, ciljevi, doprinosi i ograničenja rada.":
        "Uvod: problem, svrha, ciljevi, istraživačka pitanja IP1–IP5, doprinosi i ograničenja.",
    "Teorijski okvir: sustavi preporuke, personalizacija, implicitne povratne informacije, cold start i objašnjiva umjetna inteligencija.":
        "Teorijski okvir: sustavi preporuka, implicitne povratne informacije, cold-start, audio-ugradnje, objašnjivost i odvajanje glazbenih izvora.",
    "Strojno razumijevanje glazbe: audio-ugradnje, klasifikacija, prijenos učenja, grupiranje i odvajanje zvučnih izvora.":
        "Pregled relevantnih metoda i rješenja: sadržajno, kolaborativno i hibridno preporučivanje, prijenosno učenje, grupiranje i Demucs.",
    "Analiza zahtjeva i arhitektura platforme 808Music: korisnici, podatkovni model, verzionirani API, ML servisi i asinkrona obrada.":
        "Korisnici, zahtjevi i arhitektura 808Music: slojevi sustava, podatkovni model, API v2, ML radnici, RabbitMQ i objektna pohrana.",
    "Realizacija personaliziranog otkrivanja i slušanja: korisnički profil, kandidati, kontekstno rangiranje, cold start, starost, raznolikost, objašnjenja te Demucsov tok odvajanja i sinkronizirane reprodukcije zvučnih izvora.":
        "Metodologija istraživanja: izvori podataka, vremenski presjeci, osnovni pristupi, metrike, ablacijska analiza, tehnička evaluacija stemova i IP5.",
    "Metodologija evaluacije: skupovi podataka, podjela podataka, bazni modeli, preporučivačke metrike, tehnička provjera odvajanja izvora, analiza uklanjanja komponenti i korisnička studija.":
        "Realizacija otkrivanja: audio-analiza, klasifikacijske glave, klasteriranje, profil, hibridno rangiranje, cold-start, objašnjenja i dnevni popisi.",
    "Rezultati i rasprava: rezultati klasifikacije, grupiranja, preporučivanja, odvajanja zvučnih izvora i korisničke evaluacije te analiza kompromisa i ograničenja.":
        "Realizacija personaliziranog slušanja: Demucsovi poslovi, statusi, pohrana, potpisani resursi i sinkronizirani stem-player.",
    "Zaključak i smjernice za budući razvoj.":
        "Testiranje i rezultati: tehničke provjere, klasteriranje, preporuke, cold-start, ablacijska analiza, Demucs, sinkronizacija i IP5.",
    "Literatura.":
        "Rasprava i zaključak: tumačenje IP1–IP5, ograničenja valjanosti, arhitektonski kompromisi i smjernice za budući razvoj.",
    "Prilozi: odabrani dijagrami, konfiguracije, dodatne tablice rezultata i upute za pokretanje, ako budu potrebni.":
        "Literatura i prilozi: anketni instrument, konfiguracije, API operacije, kontrolna lista i zapis reproduktivnosti eksperimenata.",

    "8. Défossez, A. (2021). Hybrid Spectrogram and Waveform Source Separation. arXiv:2111.03600. https://arxiv.org/abs/2111.03600":
        "8. Défossez, A. (2021). Hybrid Spectrogram and Waveform Source Separation. arXiv:2111.03600. https://arxiv.org/abs/2111.03600",
    "12. Angular Team. Angular documentation. https://angular.dev/":
        "12. W3C. (2021). Web Audio API. W3C Recommendation. https://www.w3.org/TR/webaudio-1.0/",
}


SCHEDULE = [
    ["Aktivnost", "Vrijeme / rok"],
    ["Analiza literature, usklađivanje istraživačkih pitanja i konačnih zahtjeva", "5–7 dana"],
    ["Inventar podataka, zamrzavanje evaluacijskog skupa i definiranje protokola", "3–5 dana"],
    ["Funkcionalne provjere audio-analize i usporedba metoda grupiranja", "3–5 dana"],
    ["Rolling-origin evaluacija preporuka, cold-start i ablacijska analiza", "5–7 dana"],
    ["Demucsov CPU/GPU benchmark, provjera pohrane i sinkronizacije stemova", "3–5 dana"],
    ["Korisnička studija stem-playera IP5 i analiza rezultata", "7–12 dana"],
    ["Pisanje, usklađivanje rasprave i zaključka te završna tehnička provjera", "10–15 dana"],
]


def main() -> None:
    if OUTPUT.exists():
        raise FileExistsError(f"Refusing to overwrite existing output: {OUTPUT}")

    with ZipFile(SOURCE) as source_archive:
        root = etree.fromstring(source_archive.read("word/document.xml"))
        matches = {key: [] for key in REPLACEMENTS}
        for paragraph in root.xpath(".//w:p", namespaces=NS):
            value = paragraph_text(paragraph)
            if value in matches:
                matches[value].append(paragraph)

        missing = [key for key, paragraphs in matches.items() if len(paragraphs) != 1]
        if missing:
            details = "\n".join(f"{len(matches[key])}x {key[:100]}" for key in missing)
            raise RuntimeError(f"Expected exactly one paragraph for every replacement:\n{details}")

        for old, new in REPLACEMENTS.items():
            replace_paragraph_text(matches[old][0], new)

        tables = root.xpath(".//w:tbl", namespaces=NS)
        if len(tables) != 1:
            raise RuntimeError(f"Expected one schedule table, found {len(tables)}")
        rows = tables[0].xpath("./w:tr", namespaces=NS)
        if len(rows) != len(SCHEDULE):
            raise RuntimeError(f"Schedule has {len(rows)} rows, expected {len(SCHEDULE)}")
        for row, values in zip(rows, SCHEDULE, strict=True):
            cells = row.xpath("./w:tc", namespaces=NS)
            if len(cells) != len(values):
                raise RuntimeError("Unexpected number of schedule columns")
            for cell, value in zip(cells, values, strict=True):
                paragraphs = cell.xpath("./w:p", namespaces=NS)
                if not paragraphs:
                    raise RuntimeError("Schedule cell has no paragraph")
                replace_paragraph_text(paragraphs[0], value)

        first_heading = root.xpath(".//w:p[.//w:t[contains(., '1. OPIS ZAVRŠNOG RADA')]]", namespaces=NS)
        if len(first_heading) != 1:
            raise RuntimeError("Could not verify the start of the editable application body")

        document_xml = etree.tostring(root, xml_declaration=True, encoding="UTF-8", standalone="yes")
        with ZipFile(OUTPUT, "w", ZIP_DEFLATED) as output_archive:
            for item in source_archive.infolist():
                data = document_xml if item.filename == "word/document.xml" else source_archive.read(item.filename)
                output_archive.writestr(deepcopy(item), data)

    print(OUTPUT)


if __name__ == "__main__":
    main()
