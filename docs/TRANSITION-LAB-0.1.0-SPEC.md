# Transition Lab 0.1.0 — specificație înghețată

## Scop
Aplicație Windows x64 complet offline, derivată din Shotcut, redusă la un flux foto + muzică. Fișierele originale nu sunt modificate.

## Intrare
- Fotografii: JPG, JPEG, PNG.
- O singură melodie importată de utilizator.
- Miniaturi și reordonare drag-and-drop.
- Fotografiile își păstrează proporțiile; fără crop arbitrar.

## Tranziții obligatorii
1. Panouri articulate
2. Acordeon 3D
3. Cuburi rotative
4. Mozaic dreptunghiular
5. Mozaic în patru zone
6. Fâșii verticale
7. Jaluzele orizontale
8. Felii decalate
9. Diafragmă radială
10. Inel segmentat
11. Cioburi diagonale
12. Perete fotografic
13. Rame plutitoare
14. Evantai de rame
15. Blocuri din profunzime

Fiecare tranziție trebuie selectabilă între fotografii. Durata și direcția sunt reglabile unde geometria permite. Combinațiile verticale/orizontale trebuie testate separat.

## Referință vizuală
`TRANZITII(1).mp4` este folosit exclusiv pentru geometrie, formă, perspectivă și deplasare. Imaginile și celelalte elemente din videoclip nu se copiază și nu se distribuie în depozit.

## Audio
- Import melodie.
- Control volum.
- Fade In și Fade Out, implicit 2 secunde fiecare.
- Fără beat-sync în 0.1.0.

## Preview și export
- Preview în aplicație.
- MP4 H.264, Full HD 1920×1080, 25 sau 30 fps.
- NVIDIA NVENC implicit când este disponibil.
- Avertizare vizibilă obligatorie la fallback software.
- Diagnostic afișat/salvat: CPU, GPU, RAM, encoder, FPS și durata exportului.

## Funcții excluse din 0.1.0
RAW, EXIF, import video, beat-sync, subtitrări, captură, filtre artistice, blur și flash. Funcțiile Shotcut fără legătură cu fluxul cerut trebuie ascunse.

## Porți obligatorii de validare
1. Se fixează commitul Shotcut upstream.
2. Se compilează și verifică Shotcut nemodificat pe Windows x64 înaintea modificărilor.
3. Fiecare familie geometrică este implementată și testată individual.
4. Preview-ul este comparat cu exportul.
5. Sunt testate exporturile 25/30 fps, NVENC și fallback software.
6. Installerul offline este testat într-un Windows curat.
7. O funcție nu este marcată finalizată fără dovadă de test.

## Politica limitărilor
Dacă o limitare tehnică împiedică o tranziție, lucrul se oprește și problema este documentată exact înainte de orice înlocuire sau reducere a cerinței.

## Livrabile finale
1. `Transition-Lab-0.1.0-Windows-x64-Setup.exe`
2. Codul-sursă corespunzător
3. Checksum SHA-256
4. Raportul testelor
5. Lista limitărilor cunoscute
6. Instrucțiuni scurte de instalare și testare

## Versionare
- Ramură de lucru: `transition-lab-0.1.0`
- Versiune produs: `0.1.0`
- Upstream Shotcut inițial: `mltframework/shotcut@ce58962c93a283fdabc9a7617ee2a3e802f9759b`
