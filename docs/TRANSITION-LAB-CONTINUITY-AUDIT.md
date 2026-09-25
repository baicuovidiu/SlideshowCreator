# Transition Lab 0.1.0 — stare persistentă și audit

Ultima actualizare: 2026-09-24  
Ramură: `transition-lab-0.1.0`  
Depozit: `baicuovidiu/SlideshowCreator`

Acest document este sursa obligatorie de continuitate. Proiectul nu trebuie reluat numai din conversație.

## Obiectivul imediat

Livrarea unui installer Windows x64 offline care, înainte de extinderea bibliotecii de tranziții:

1. importă JPG/JPEG/PNG;
2. afișează și reordonează miniaturi;
3. redă preview-ul slideshow-ului;
4. importă muzică și aplică volum plus Fade In/Fade Out;
5. exportă efectiv slideshow-ul în MP4 H.264;
6. păstrează fișierele originale nemodificate.

O funcție nu este „finalizată” doar pentru că se compilează. Exportul este finalizat numai după crearea și verificarea unui MP4 real prin traseul aplicației.

## Baza tehnică verificată

- Shotcut este fixat la commitul `ce58962c93a283fdabc9a7617ee2a3e802f9759b`.
- Pluginul `frei0r.transitionlab_panels` se compilează, este împachetat și este recunoscut de MLT.
- Testele geometriei pentru cele patru direcții ale primei tranziții au trecut.
- Renderul MLT de test pentru tranziția Panouri articulate produce MP4.
- Installerul Inno Setup se construiește și trece instalarea silențioasă într-un director Windows curat.
- Buildul #25 a reușit, dar testul utilizatorului a confirmat că exportul din interfață nu funcționează. Prin urmare #25 NU este o versiune funcțională.

## Implementare existentă

Patchurile sunt aplicate în ordine din directorul `patches/`.

- 0001: panoul Transition Lab și interfața de bază.
- 0002: setări de tranziție pentru fiecare pereche de fotografii.
- 0003–0008: pluginul Panouri articulate, împachetare și teste de direcție.
- 0009: specificațiile tranzițiilor per pereche.
- 0010: preview cu motorul MLT.
- 0011: pregătirea slideshow-ului pentru export.
- 0012: muzică, volum și fade-in/fade-out.
- 0013: încercare de pornire a exportului prin `EncodeDock::on_encodeButton_clicked`.
- 0014: mutarea comenzilor Preview și Export MP4 într-o zonă vizibilă.
- 0015: marchează slideshow-ul cu `kExportFromProperty`, astfel încât EncodeDock să aleagă slideshow-ul, nu timeline-ul gol. Patchul a fost compilat și validat automat în buildul #29; fluxul GUI instalat rămâne de confirmat practic.

## Greșeli confirmate și lecții obligatorii

### 1. Compilarea a fost confundată cu funcționarea

Buildurile au verificat compilarea, pluginul, renderul MLT separat și instalarea, dar nu traseul complet din interfață. Utilizatorul a primit versiuni în care exportul nu funcționa.

Regulă: nu se declară export funcțional fără MP4 creat și verificat prin fluxul aplicației sau printr-un test echivalent care folosește exact aceeași cale de cod.

### 2. Primul buton de export nu pornea exportul

Implementarea inițială doar deschidea panoul Shotcut Export. Nu iniția dialogul și jobul de export.

Corecția 0013 a invocat slotul de export, dar nu a fost suficientă.

### 3. Butonul Export era în afara zonei vizibile

Comenzile Preview și Export erau sub setări și diagnostice, într-un dock fără derulare. Pe configurația utilizatorului butonul nu apărea.

Corecția 0014 mută butoanele lângă Preview, înaintea diagnosticelor.

### 4. Exportul selecta sursa greșită

Auditul codului Shotcut a arătat că `EncodeDock::onProducerOpened()` selectează implicit primul element din `fromCombo`. Când timeline-ul există, acesta devine sursa implicită. Slideshow-ul construit de Transition Lab era deschis, dar nu era marcat cu `kExportFromProperty`. Exportul putea folosi timeline-ul gol în locul slideshow-ului.

Corecția 0015 setează:
`slideshow->set(kExportFromProperty, 1);`

### 5. Patchuri invalide trimise fără validare locală

Buildul #24 a eșuat cu `corrupt patch at line 31` în patchul 0014, din cauza numărului greșit de linii din hunk.

Buildul #26 a eșuat cu `corrupt patch at line 22` în patchul 0015, din aceeași categorie de eroare.

Regulă: fiecare patch nou trebuie verificat cu `git apply --check` pe sursa rezultată după toate patchurile anterioare înainte de pornirea buildului lung. Nu se mai scriu manual contoare de hunk fără verificare.

### 6. Testul de instalare nu validează funcția de export

Instalarea silențioasă confirmă doar că EXE-ul și DLL-urile sunt copiate. Nu confirmă că utilizatorul poate crea un MP4.

Regulă: raportul trebuie să separe explicit:
- compilare;
- instalare;
- pornire;
- preview;
- export MP4;
- verificare codec/rezoluție/FPS/audio.

## Istoric relevant al buildurilor

- #12: succes; artefact 215.107.928 bytes; SHA-256 arhivă `21fa275ba128d0654d4b0541138942567fd36c6cf01d7b3d0304d399f4cc1504`.
- #13: compilarea a trecut, verificarea PowerShell a fost greșită.
- #14: succes; MLT a enumerat pluginul; patru direcții testate; SHA-256 arhivă `fc1feb94f646e31707ec98555139ce739ee4d614054750d79919edc676f7b404`.
- #16: integrarea preview-ului.
- #21: primul installer pilot; preview-ul a funcționat la utilizator, exportul nu.
- #23: succes tehnic; SHA-256 EXE `5689271E627C1BC136AC1E74B7ED3EA1D375B720B8DF5541FF597A65FA98F8D7`; utilizatorul nu a avut un export funcțional.
- #24: eșec înainte de compilare; patch 0014 corupt.
- #25: succes; installer SHA-256 `5EB86C6D3962AAAC5E78404C63BBBCD1D27F4FE2C655F1553603A33AD0194B2B`; butonul a devenit vizibil, dar exportul nu a funcționat la utilizator.
- #26: eșec înainte de compilare; patch 0015 corupt.
- #27: succes după corectarea structurii patchului 0015.
- #28: eșec înainte de compilare; definiția testului MP4 a fost coruptă de o înlocuire textuală care a interpretat secvența `$'` din expresiile regulate. Aplicația nu a fost compilată în acest build.
- #29: succes integral; patchurile, geometria, compilarea, renderul MP4 H.264 1920×1080 la 25 fps, verificarea cu ffprobe, construirea installerului și instalarea curată au trecut. SHA-256 EXE: `512A521FEC32E2D96E45880FA084EB36FD6890B1BD9792EE64408C663060E2E7`. SHA-256 artefact ZIP: `81b7fc7238e7bf539fdf3c05b53d1ac983b6149757f605498d8ba6abbffb0cd3`.

## Stare funcțională adevărată

Verificat practic de utilizator pe versiuni anterioare (#21–#25):
- instalare: DA;
- import patru fotografii: DA;
- preview: DA;
- export MP4: NU în acele versiuni; #38 a trecut testul automat, proba pe calculatorul utilizatorului este în așteptare;
- cele 15 familii de tranziții: NU; doar Panouri articulate are motor implementat/testat parțial;
- audio în export: NU este confirmat practic;
- NVENC implicit și avertizarea de fallback: NU sunt confirmate;
- diagnostice complete CPU/GPU/RAM/encoder/FPS/timp: NU sunt complete.

## Poarta obligatorie înainte de următorul installer

Nu se livrează un alt installer drept „funcțional” până când sunt îndeplinite toate:

1. toate patchurile trec `git apply --check`;
2. compilarea Windows trece;
3. testul tranziției trece;
4. se produce un MP4 H.264 real din minimum două surse;
5. fișierul este verificat ca existent și mai mare decât pragul minim;
6. codec, rezoluție și FPS sunt inspectate;
7. installerul trece instalarea curată;
8. limitarea rămasă — imposibilitatea automatizării complete a clicurilor GUI pe runner — este declarată explicit;
9. verificarea practică a utilizatorului rămâne necesară înainte de declararea fluxului UI ca finalizat.

## Următorul pas

Această secțiune descria situația de după #29 și este înlocuită de actualizarea pentru #38 de la finalul documentului. Următorul pas este verificarea practică a pilotului #38 cu fotografiile utilizatorului, apoi extinderea treptată a tranzițiilor și validarea audio, NVENC și concordanței preview/export.

## Actualizare 24 septembrie 2026 — diagnostic export prin aplicația instalată

- #30 și #31 au compilat și instalat; patchul 0016 marchează playlistul și tractorul final ca surse exportabile. Aceasta nu dovedește că butonul creează MP4.
- #33 a eșuat înainte de job din cauza YAML corupt la inserarea testului; corectat în #34.
- #34 și #35 au construit/instalat aplicația, dar testul din aplicația instalată nu a produs MP4 în opt minute. #35 a confirmat afișarea ferestrei Shotcut.
- #36 a identificat exact punctul în care exportul devine inert: testul a încărcat două PNG, butonul Export MP4 a fost activ, `createSlideshow()` a returnat un pointer, semnalul `exportRequested` a fost emis; la intrarea în `EncodeDock::on_encodeButton_clicked()`, `MLT.producer()` era nul. Funcția revine imediat din prima condiție. Link: https://github.com/baicuovidiu/SlideshowCreator/actions/runs/35998007381 .
- #37, commit `35249497d6aaf4ba3599e965e1cc80b8cc6cac58`, instrumentează validitatea și lungimea slideshow-ului înainte de `MainWindow::open()` și sursa MLT imediat după deschidere și după selecția sursei. Link: https://github.com/baicuovidiu/SlideshowCreator/actions/runs/36007054183 . Rezultat #37: `MLT.producer()` rămâne valid imediat după `open()` și `onProducerOpened()`, dar devine nul până la execuția apelului amânat al exportului. Aceasta localizează pierderea sursei în intervalul dintre apelul amânat și execuția lui.

Nu modificați `force_seekable` pe baza unei presupuneri: eroarea confirmată este un producător nul înainte de verificarea seekability. Distingeți un slideshow invalid la intrarea în `open()` de eliminarea lui în `open()` ori în callbackurile `producerOpened` folosind marcajele din #37. Exportul din aplicația instalată a trecut automat în buildul #38, conform verificărilor consemnate mai jos. Testul actual rulează cu două PNG generate pe runner și cu calea fișierului transmisă prin variabilă de mediu; el nu automatizează alegerea fișierelor prin dialogurile Windows și nu înlocuiește proba cu fotografiile utilizatorului.

## Actualizare 24 septembrie 2026 — pilotul #38 validat

- Commit sursă: `f169edc00526845fd08483e9f20ea50bf6489a90`. Patchul 0020 pornește slotul de export sincron, cât slideshow-ul este încă deschis; #37 arătase că sursa se închidea înainte de apelul amânat.
- Rulare Windows: https://github.com/baicuovidiu/SlideshowCreator/actions/runs/36023443954 — succes, inclusiv etapa „Verify and package foundation” și încărcarea artefactului.
- Test automat prin aplicația instalată: două PNG sintetice sunt importate prin aceeași cale a panoului; aceeași comandă ca butonul Export MP4 este declanșată programatic; un MP4 real este generat și verificat cu ffprobe pentru H.264, 1920×1080 și 25 sau 30 fps. Mesajul din log: `Installed application Export MP4 end-to-end test passed`.
- Installer: `Transition-Lab-0.1.0-Windows-x64-Setup.exe`. SHA-256 înregistrat de runner: `0258DC9FAC09CD574469C0B91DE5CB2BF81F4CCDB96AD69286CEADDF25BA32F5`.
- Artefact ZIP GitHub: https://github.com/baicuovidiu/SlideshowCreator/actions/runs/36023443954/artifacts/10822980093 — 354.134.935 bytes, digest declarat de GitHub `sha256:abe8e6af02a4ce8b6252d4cee5996c4ab4b8ea5e86becc86acd3a6e0773c75d6`, expiră 24 octombrie 2026.
- Limitare a verificării: mediul de lucru nu poate descărca local arhiva de 354 MB (limită de transfer 32 MiB), deci integritatea ei locală nu a fost recalculată aici. Testul automat a verificat instalarea și codificarea; nu a făcut clic fizic pe buton și nu a folosit fotografiile utilizatorului. Audio, NVENC, restul tranzițiilor și compararea vizuală preview/export nu sunt validate de #38.

Acest build este primul pilot cu exportul confirmat prin traseul aplicației instalate. Păstrați patchurile și testul instalat drept bază pentru extinderea ulterioară; nu declarați Etapa 1 completă înainte de verificarea tuturor cerințelor.

## Actualizare 24 septembrie 2026 — muzica în exportul instalat

- #39 a eșuat la parsarea workflow-ului înainte de pornirea jobului: înlocuirea de text pentru testul audio a interpretat secvența `$'` din regex drept sufixul șirului. Fișierul a fost reconstruit folosind o funcție de înlocuire literală și verificat cu parser YAML înainte de #40. Niciun installer #39 nu a fost produs.
- #40, commit `a5e48d44a2c74861e422a2d06a575a10f0fdebde`, a trecut: aplicația instalată a exportat MP4 cu o pistă audio AAC verificată de ffprobe. Link: https://github.com/baicuovidiu/SlideshowCreator/actions/runs/36032790703 .
- Installer #40 SHA-256 calculat de runner: `D533BBAEF7558E039B057FF0C6876A66F211DAF02F52823CAFBF4B45F67B5945`. Artefact: https://github.com/baicuovidiu/SlideshowCreator/actions/runs/36032790703/artifacts/10825646671 — 354.305.509 bytes, digest GitHub `sha256:3e9db9bb1544e0024bb41444406b2c99b2c13ae0d812f39f21c815e0ce614aa7`.
- Conținutul audio este confirmat ca pistă AAC, dar intensitatea și fade-urile implicite de două secunde nu au fost măsurate de #40. Un test ulterior compară nivelul la început, mijloc și final. Testele folosesc un semnal sintetic; proba cu melodia utilizatorului rămâne necesară.

## Actualizare 24 septembrie 2026 — Fade Out absent în buildul #41

- Rularea https://github.com/baicuovidiu/SlideshowCreator/actions/runs/36039597095 a compilat și instalat aplicația, dar a eșuat în verificarea fade-urilor: început −75 dB, mijloc −28 dB, final −27,8 dB. Pista AAC există, Fade In este măsurabil; Fade Out nu este. Installerul #41 nu este validat pentru audio.
- Ipoteza tehnică verificată prin citirea codului: WAV-ul de test are 12 secunde, iar slideshow-ul este mai scurt; tractorul audio/video nu avea limită proprie, deși filtrele și pista audio erau limitate la lungimea fotografiilor. Fixul #42 setează explicit punctul final al tractorului la ultimul cadru al slideshow-ului. Efectul rămâne neverificat până la testul Windows.
- Nu livrați #41 ca versiune reparată; confirmați atenuarea la final numai după trecerea măsurătorilor din aplicația instalată.

## Actualizare 24 septembrie 2026 — testul #42

- #42 https://github.com/baicuovidiu/SlideshowCreator/actions/runs/36049627067: compilarea și instalarea au trecut, dar testul Fade Out a repetat rezultatul #41: început −75 dB, mijloc −28 dB, final −27,8 dB. Limitarea inițială a tractorului a fost insuficientă; nici acest installer nu este validat pentru fade.
- Următoarea corecție fixează și lungimea pistei audio și reconfirmă lungimea tractorului după plantarea mixului; testul măsoară explicit durata MP4-ului și respinge exporturile mai lungi decât cele două fotografii. Se imprimă și lungimile producătorilor MLT la eșec. Acestea sunt ipoteze testate de următorul build, nu rezultate declarate.

## Actualizare 25 septembrie 2026 — diagnosticul #43, încercarea 2

- Compilarea și instalarea au trecut. Exportul instalat are 8,490667 secunde, exact 212 cadre pentru slideshow, pistă audio și rezultat. Ipoteza duratei excesive este infirmată.
- Valorile rămân început −75 dB, mijloc −28 dB, final −27,8 dB: Fade In funcționează, Fade Out din animația combinată nu este aplicat.
- Următoarea corecție folosește trei filtre MLT independente: volum constant, rampă Fade In și rampă Fade Out. Aceasta urmează structura filtrelor audio native Shotcut. Rezultatul rămâne nevalidat până la următorul test Windows instalat.

## Actualizare 25 septembrie 2026 — rezultatul #44

- #44 a compilat și instalat aplicația, dar a repetat: MP4 8,490667 secunde, început −75 dB, mijloc −28 dB, final −27,8 dB. Nici separarea în animații multiple pe proprietatea `level` nu a produs Fade Out.
- Următoarea corecție folosește exact mecanismul presetului MLT `volume/fade_out`: filtrul este activ numai în ultimele două secunde, cu `gain=1` și `end=0`. Fade In folosește simetric `gain=0`, `end=1`, iar volumul general rămâne filtru separat. Rezultatul este nevalidat până la următorul build.

## Actualizare 25 septembrie 2026 — rezultatul #45

- #45 a compilat și instalat aplicația, dar Fade Out a rămas absent. MP4-ul are 8,490667 secunde și 212 cadre; valorile măsurate au fost început −41,4 dB, mijloc −26,9 dB, final −26,7 dB. Rampa nativă MLT bazată pe `volume gain/end` nu atenuează finalul acestei surse audio.
- Următoarea corecție înlocuiește exclusiv rampele de fade cu filtrul FFmpeg dedicat `avfilter.afade`, configurat în secunde pe întregul flux audio. Dacă filtrul nu este disponibil în pachetul MLT, construirea slideshow-ului eșuează explicit, în loc să exporte fără fade. Rezultatul este nevalidat până la testul Windows instalat.

## Actualizare 25 septembrie 2026 — rezultatul #46

- #46 a compilat și instalat aplicația, iar exportul are în continuare 8,490667 secunde și 212 cadre. Filtrul `avfilter.afade` a primit 50 de cadre pentru fiecare fade și startul Fade Out la cadrul 162, dar măsurarea a rămas început −40,3 dB, mijloc −26,8 dB, final −26,7 dB. Fade Out nu este aplicat; installerul #46 nu este validat.
- Diagnosticul indică faptul că filtrul atașat direct sursei audio nu urmărește capătul cronologiei exportate. Următoarea corecție mută o singură anvelopă de volum animată pe tractorul final, după compunerea pistelor. Astfel, pozițiile 0–211 sunt exact pozițiile MP4-ului. Rezultatul este nevalidat până la testul Windows instalat.
