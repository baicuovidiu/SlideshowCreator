# Slideshow Creator — SPECIFICAȚIE CANONICĂ

Versiune document: 2026-09-19
Bază de dezvoltare: MVP 0.1.10
Statut: documentul de referință pentru toate versiunile următoare.

## 1. Principiul fundamental
Aplicația este un editor Windows independent pentru slideshow foto-video de calitate, orientat în primul rând spre fotografie profesională. Regula nenegociabilă: motorul automat nu sacrifică fotografia pentru efect.

- Fișierele originale nu se modifică.
- Fără crop automat distructiv.
- Fără zoom automat arbitrar.
- Fără blur sau filtre care reduc claritatea/acuratețea.
- Fotografia se scalează proporțional cu CONTAIN; spațiul liber este preferabil tăierii subiectului.
- Exportul final folosește materialele originale; proxy-urile sunt doar pentru preview.
- O funcție nu este „gata” până nu este testată cu materialele reale ale utilizatorului.

## 2. Livrare și instalare
- Windows, funcționare offline după instalare.
- Un singur installer EXE self-contained; utilizatorul nu instalează separat .NET, Python, FFmpeg sau codec packs.
- FFmpeg și runtime-urile necesare sunt locale aplicației.
- Upgrade in-place între versiuni, cu păstrarea proiectelor și setărilor.
- Proiectele trebuie să supraviețuiască dezinstalării.
- Fiecare build de test are versiune distinctă în EXE, UI, installer și numele artifactului.
- Fără activare online artificială; instalabil pe mai multe laptopuri.

## 3. Moduri de lucru
Același motor deservește:
- FOTO + VIDEO
- DOAR FOTO
- DOAR VIDEO

Video: import, ordonare, preview, TRIM IN/OUT non-distructiv, planuri independente, muzică, audio original Mute/Original, export.

## 4. Import, cronologie și media
- Import cumulativ: loturile noi se adaugă, nu înlocuiesc ce este deja în proiect.
- Fotografii: EXIF DateTimeOriginal ca sursă cronologică principală; fallback controlat.
- Offset de timp per cameră păstrat numai în proiect; EXIF original nu se modifică.
- Video: creation_time/container metadata, apoi fallback la data fișierului.
- Sortare cronologică automată/la cerere; reordonarea manuală rămâne până la o re-sortare explicită.
- RAW și formate foto/video largi prin stack-ul multimedia al aplicației.
- Thumbnail-uri vizibile, nu doar nume de fișiere.

## 5. Timeline și interfață
- Timeline de bază, panouri redimensionabile.
- Zone/comenzi grupate vizual.
- Zona mare IMPORT este clickabilă și acceptă drag & drop.
- Preview clip selectat separat de PREVIEW MONTAJ COMPLET.
- UI trebuie să arate versiunea curentă.
- La export: progres REAL 0–100%, etapa curentă, elemente procesate (ex. 47/120) și ETA/timp rămas.
- La export finalizat CU SUCCES: clopoțel audio scurt/elegant. Nu se redă la eroare.

## 6. TRANZIȚII
TRANZIȚII = efecte simple între cadre, separate conceptual de CARUSEL.
Exemple: Crossfade, Dissolve, Slide, Wipe, diagonale.
- Mix automat: Elegant / Alternativ / Dinamic.
- Alegere manuală per element.
- Durate valide FFmpeg, formatate invariant (ex. 0.65, niciodată .65).
- Intro vizual fluid la primul cadru și outro fluid la ultimul.

## 7. CARUSEL — definiție
CARUSEL = mini-montaj animat în interiorul montajului, cu 2–4 planuri/obiecte independente simultan. Nu există categorie separată „COMPOZIȚII”; funcționalitatea complexă aparține CARUSEL.

Fiecare plan are conceptual:
- sursă FOTO sau VIDEO;
- poziție și dimensiune;
- StartOffset / EndOffset;
- direcție de intrare și ieșire;
- mișcare proprie;
- Z-order/prioritate vizuală;
- keyframes;
- timing independent.

Regulă: FOTO → scale down/contain → poziționare → plan/cadru → animație. Niciodată FOTO → crop ca să umple planul.

## 8. CARUSEL — mișcări convenite
Trebuie să existe și să poată fi variate controlat:
- 2 Orizontal Opus;
- 2 Orizontal Paralel;
- 2 Orizontale Fluide;
- 2 Orizontale Alternante;
- Diagonal / Opus;
- Diagonal \ Opus;
- 4 Cadrane → 1;
- 4 → 2 → 1;
- ulterior 1 → 4 → 1, 2 → 4 → 2 și familii 3-plan.

Exemplu obligatoriu: planul de sus intră din STÂNGA, planul de jos intră din DREAPTA; se întâlnesc, coexistă, apoi continuă jocul. Varianta inversă și alte combinații sunt eligibile.

Intrările pot fi:
- simultane sau decalate;
- uniforme (1→2→3→4, interval/viteză egală);
- neuniforme, cu întârzieri/viteze controlat variate;
- stânga/dreapta/sus/jos/diagonal.

Ieșirile sunt independente. Un plan poate rămâne și deveni Full Frame.

## 9. Alternarea după suprapunere
Două imagini care se încalecă nu rămân cu una permanent eclipsată.
După întâlnire urmează aproximativ 2 secunde de alternare a priorității:
- A favorizată;
- apoi B;
- apoi A/B din nou, cu intervale ușor neuniforme (~0.45/0.65/0.55 s).
Schimbarea nu trebuie să fie flicker sau simplu swap brutal de Z-order; este însoțită de mic slide/reveal/repoziționare. Ambele imagini trebuie să devină clar lizibile. Concept: „dans al planurilor”.

## 10. Patru imagini și layout-uri estetice
Motorul trebuie să suporte:
- 2×2 egal;
- 60/40;
- o imagine mare + 2 mici;
- o imagine mare + 3 mici;
- aranjamente asimetrice inspirate din colajele de referință;
- patru imagini care se alătură, apoi compoziția se sparge 4→2→1.

Cele patru sunt obiecte independente, nu un colaj pre-randat. Pot fi în viitor FOTO+FOTO+VIDEO etc.
La 4→2→1: patru intră/formeză compoziția; două ies; două rămân/repoziționează; una pleacă; supraviețuitoarea evoluează fluid spre Full Frame.

## 11. Diagonale
Diagonala este permisă numai în forme sigure:
- fotografii dreptunghiulare COMPLETE aranjate diagonal/overlap;
- intrări/ieșiri pe traiectorii diagonale;
- geometrie/layout/fundal diagonal.
Nu se taie fotografia pe diagonală și nu se maschează distructiv oameni/corpuri.

## 12. Automatizare CARUSEL
CARUSEL AUTOMAT / Complex Automat:
- alege numai preseturi eligibile pentru numărul de surse disponibile;
- fără repetare consecutivă mecanică;
- variație controlată a direcțiilor, ordinii și timingului;
- păstrează override-urile manuale;
- poate reveni la Full Frame;
- nu folosește variante care încalcă regula fotografiei complete.

## 13. FOTO + VIDEO în CARUSEL
Arhitectura trebuie să permită:
- video sus continuu + fotografii jos care se schimbă;
- fotografii sus + video jos;
- două video simultan;
- fiecare plan cu durată independentă;
- TRIM-ul video respectat exact.
Acest lucru nu trebuie modelat ca două surse consumate rigid pentru aceeași durată, ci ca segment CARUSEL cu planuri independente.

## 14. VIDEO TRIM și audio original
Pentru fiecare video:
- TRIM IN + TRIM OUT non-distructiv;
- preview cu Play/Pause și seek;
- timeline/bară cu mânere IN/OUT (ținta UX, nu doar câmpuri numerice);
- ulterior reglaj fin ±0.1 s.
TRIM stabilește secvența vizuală; Mute/Original controlează separat audio-ul original al secvenței tăiate.

## 15. Muzică și beat
- Muzică/audio adăugată de utilizator.
- Fade-in audio la început, fade-out la final.
- Beat engine de bază: BPM/beat detection și sincronizarea schimbărilor relevante.
- Evoluție: waveform, beat strength, structură muzicală, density presets, markere manuale.
- Nu fiecare beat obligă schimbarea imaginii.

## 16. Preview
- Preview rapid/proxy separat de exportul final.
- Preview-ul trebuie să reprezinte montajul complet și CARUSEL-urile.
- Proxy la rezoluție redusă, fără upscale inutil la Full HD în pipeline-ul de preview.
- Preview-ul montajului nu are voie să modifice SourceDuration/TRIM-ul clipului selectat.
- Controalele de trim nu trebuie să opereze accidental pe preview-ul montajului.

## 17. Export și performanță
Configurația țintă actuală include i9-13900H + RTX 4060 Laptop.
- NVENC trebuie TESTAT efectiv la runtime, nu doar detectat în lista FFmpeg.
- Fallback automat CPU/Intel/AMD unde este posibil.
- Settings > Performance: Redus / Normal / Maxim; Normal implicit.
- opțiune „Performanță maximă la export”.
- fără prioritate Realtime.
- obiectivul este viteza reală, nu procent mare de GPU de dragul procentului.

Reconstrucția exportului trebuie să:
- reducă drastic numărul de recodări;
- elimine intermediarii inutili;
- combine operațiile compatibile într-un singur filter graph/pass;
- evite normalizarea tuturor fotografiilor în MP4 înainte de montaj când nu este necesar;
- folosească hardware encode unde este sigur;
- păstreze calitatea și regula contain/no-crop.

Benchmark de regresie: testul din 2026-09-19, 120 fotografii + Complex Automat + muzică, pornit 17:38 și eșuat la 18:20 (~42 min) din cauza xfade duration '.65'. Acest comportament este INACCEPTABIL. 0.1.10+ trebuie măsurat pe același tip de proiect.

Ținta orientativă pentru un pipeline matur pe hardware-ul țintă: ordinul câtorva minute pentru 120 fotografii Full HD, fără promisiune de multiplicator până la măsurare reală.

## 18. Stabilitate și validare înainte de export lung
- Parametrii FFmpeg trebuie validați înainte de randarea lungă.
- Duratele folosesc separator zecimal invariant și zero înaintea zecimalelor.
- CARUSEL Automat nu poate selecta 4-plan dacă nu mai există 4 surse.
- Erorile trebuie să oprească elegant procesul și să indice etapa.
- Installerul încearcă închiderea aplicației vechi pentru upgrade, fără taskkill brutal.

## 19. Starea 0.1.10 la momentul acestei specificații
Implementat/în lucru în cod:
- branch mvp-0.1.10;
- corecție FFmpeg .65 → 0.65 și formatter sigur;
- 2 Orizontale Alternante;
- 4→2→1;
- preseturi CARUSEL expuse în UI;
- allocator automat verifică eligibilitatea 4-plan;
- NVENC test real la runtime + fallback;
- progres determinat de bază;
- clopoțel la succes;
- TRIM IN corectat;
- intro/outro + muzică reunite într-o singură trecere finală;
- versiune 0.1.10 distinctă.

NEFINALIZAT / obligatoriu în etapele următoare:
- reconstrucția majoră single/fewer-pass pentru viteză;
- progres bazat și pe progresul FFmpeg + ETA real, nu doar procente de etapă;
- layout engine reutilizabil și estetic pentru 2–4 planuri;
- alternare Z-order/reveal mai sofisticată;
- segment model real CarouselSegment/CarouselPlane;
- video continuu într-un plan în timp ce alt plan schimbă fotografii;
- Mute/Original audio real;
- beat engine;
- EXIF DateTimeOriginal și offset camere;
- Performance modes;
- project save/Pack Project/Relink;
- protecția preview montage vs clip preview;
- eliminarea nested black padding/intermediarelor;
- validare real-media a 0.1.10.

## 20. Regula de dezvoltare
Acest fișier este sursa canonică de adevăr. La fiecare idee acceptată:
1. se adaugă aici;
2. se marchează implementată doar după cod;
3. se marchează validată doar după test cu material real;
4. nicio versiune nouă nu elimină tacit o cerință existentă.


## 21. CARUSEL Engine 2 — decizie de reconstrucție (2026-09-19)
Testul real 0.1.10 a arătat că actualul CARUSEL este prea banal/repetitiv și că suprapunerile pot obtura din nou o fotografie. Implementarea existentă NU este considerată conformă.

CARUSEL Automat nu mai este definit ca alegere dintr-o listă mică de preseturi. Devine motor parametric de coregrafie a planurilor. Variația se generează controlat din:
- geometrie/layout: 50/50, 60/40, 70/30, mare+mic, mare+2 mici, mare+3 mici, 2×2, L/T, asimetric, diagonal sigur;
- număr de planuri: 2–4;
- ordine și direcții independente de intrare/ieșire;
- timing uniform/neuniform, stagger și viteze diferite;
- repoziționare și schimb de roluri;
- prioritate vizuală/Z-order dinamică;
- familii de evoluție: 4→2→1, 4→1, 2→4→2, 1→4→1, 2→1→2, 1→3→1;
- revenire fluidă la Full Frame.

REGULĂ DE VIZIBILITATE: o fotografie aflată în spate nu poate rămâne inutil obturată. Dacă planurile se suprapun, motorul trebuie să alterneze prioritatea prin Z-order + repoziționare/reveal, astfel încât fiecare imagine să aibă un interval clar de lectură. Simplul overlay static nu este acceptabil.

REGULĂ DE VARIETATE: un montaj lung nu trebuie să pară aceeași schemă repetată. Motorul ține istoric al geometriei, direcțiilor și familiei de mișcare și evită repetițiile apropiate.

REGULĂ DE CALITATE: toate variațiile păstrează fotografia completă prin contain; nici creativitatea, nici geometria nu justifică crop automat distructiv.

Această secțiune este obligatorie pentru 0.1.11+ și are prioritate față de implementarea veche bazată pe câteva preseturi fixe.


## 22. CARUSEL de ansamblu / WALL — multe fotografii simultan
Motorul trebuie să poată trece temporar de la CARUSEL-ul de 2–4 planuri la compoziții de ansamblu cu multe fotografii simultan. Pentru un proiect de 120 de fotografii poate exista, la momente alese automat sau manual, o secvență în care zeci sau chiar toate cele 120 apar împreună în același canvas.

Nu este un colaj static. Fiecare fotografie rămâne obiect independent și poate intra, ieși, aluneca sau schimba poziția. Camera/canvas-ul poate parcurge ansamblul fără zoom automat aplicat fotografiei individuale.

Familii obligatorii de variații:
- PHOTO WALL: grilă dinamică cu multe fotografii complete;
- FILM STRIP / BANDĂ: rânduri care se deplasează în sensuri opuse;
- CONVEYOR: fotografii care traversează canvas-ul în flux;
- MOSAIC FLOW: mozaic cu dimensiuni diferite care se rearanjează;
- PARADE: succesiuni de fotografii care intră din margini și circulă;
- RIVER: 2–4 fluxuri independente de imagini;
- ORBIT / RING: grupuri dispuse pe traseu circular/eliptic fără deformarea surselor;
- CASCADE: apariții succesive care construiesc un perete și apoi îl desfac;
- WALL → FOCUS: multe imagini coexistă, apoi una este favorizată și devine Full Frame;
- FOCUS → WALL: Full Frame se retrage într-un ansamblu mare;
- WAVE: rânduri/coloane se deplasează cu faze diferite;
- SHUFFLE: repoziționare controlată a mai multor planuri;
- CROSS TRAFFIC: grupuri care circulă pe axe diferite fără obturare permanentă.

Pentru proiecte mari, motorul poate folosi 6, 8, 12, 16, 20, 24, 30+ sau toate fotografiile disponibile într-o secvență de ansamblu. Numărul se alege în funcție de lizibilitate, durată și rezoluția finală.

REGULĂ: „toate una lângă alta” nu înseamnă că toate trebuie să fie simultan suficient de mari pentru examinare individuală. Secvența poate plimba viewport-ul/canvas-ul printr-un wall mai mare decât cadrul 1920×1080, astfel încât fotografiile să devină lizibile pe parcurs. Fotografiile individuale rămân complete (contain), fără crop distructiv.

CARUSEL Automat trebuie să combine aceste familii cu motorul 2–4 planuri și Full Frame, generând zeci de combinații, cu memorie anti-repetiție pentru familie, geometrie, direcție, ritm și ordine. Un montaj lung trebuie să evolueze vizual, nu să repete trei formule.


### 22.1 Mișcări cinematice suplimentare pentru WALL/CARUSEL
Obligatoriu:
- ROTATION FLOW: grupuri sau benzi de fotografii complete se rotesc lent ca ansamblu; rotația nu deformează și nu cropează fotografia individuală.
- DIAGONAL SLIDE PARADE: 3–12+ fotografii complete defilează simultan pe diagonală, ca diapozitive independente, cu spațiere controlată; pot intra dintr-un colț și ieși prin colțul opus.
- DIAGONAL FILM STRIP: bandă virtuală înclinată cu fotografii necropate, deplasată continuu prin cadru; pot exista două sau mai multe benzi cu sensuri/viteze diferite.
- ROTATING WALL: wall/mozaic mai mare decât viewport-ul se poate roti lent în timp ce viewport-ul îl traversează.
- FAN / EVANTAI: mai multe fotografii complete se deschid în evantai prin rotație + translație, apoi se strâng sau una devine Full Frame.
- CARD STREAM: fotografii complete trec succesiv ca diapozitive/carduri, cu rotații discrete individuale și fără obturare permanentă.
- SPIRAL / ARC FLOW: grupurile urmează arce/spirale largi; fiecare fotografie rămâne dreptunghi complet și lizibil.
- DIAGONAL CROSSING: două fluxuri diagonale independente se intersectează cu prioritate/reveal controlată, fără ca un flux să ascundă permanent celălalt.

Rotația poate aparține planului, grupului sau canvas-ului virtual. Trebuie să existe limite estetice configurabile: rotații mici/elegante pentru fotografiile individuale și rotații mai ample numai pentru mișcarea ansamblului. Motorul automat variază sensul, unghiul, viteza, stagger-ul, distanța și numărul de imagini și evită repetarea apropiată.

Toate aceste familii respectă regula FOTO COMPLETĂ / CONTAIN / FĂRĂ CROP AUTOMAT DISTRUCTIV.
