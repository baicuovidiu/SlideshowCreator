# Motor 2.0 — audit tehnic integral (2026-09-20)

Scop: nicio etapă nu este declarată validată doar pentru că compilează. Auditul separă compile-time, runtime D3D12, hardware RTX/NVENC și testul real cu media utilizatorului.

## Verificat și corectat în audit
- Compile Gate #94 pentru frames-in-flight: SUCCESS.
- Architecture Guard: corectat; vechiul Select-String trata lista de termeni ca un singur literal și putea rata regresii.
- D3D12 RTV: eliminat descriptorul RTV global unic. Fiecare render target are acum propriul RTV heap, necesar când există cadre simultan în zbor.
- Shader root visibility: constantele b0 sunt folosite și de pixel shader; vizibilitatea a fost schimbată din VERTEX în ALL.
- Opacity blending: eliminată multiplicarea RGB cu opacity înainte de straight-alpha blend, care ar fi aplicat opacitatea de două ori.
- Texturile încă fac tranziția COPY_DEST -> PIXEL_SHADER_RESOURCE înainte de sampling.
- Draw submission rămâne batched per frame și ordonat după Z.
- Triple buffering păstrează câte un command allocator/fence per slot.

## Probleme/blocaje încă NEVALIDATE sau de reparat înainte de testul utilizatorului
1. Runtime D3D12 pe RTX 4060 nu a fost încă executat; GitHub compile gate nu dovedește randare corectă.
2. Render-target lifetime: compositorul alocă un target per frame, dar contractul encoderului nu confirmă încă momentul sigur de release după NVENC. Trebuie ownership/fence explicit.
3. SRV lifetime: descriptorii sunt alocați monoton până la 4096; eviction/release nu reciclează slotul și maparea resursei trebuie curățată fence-safe.
4. Upload path este încă sincron/blocant și creează upload resource + allocator/list per upload; corect pentru proof, nu pentru performanța finală.
5. Scheduler-ul actual este secvențial la nivel decode/upload/compose/encode; frames-in-flight nativ nu înseamnă încă pipeline complet asincron.
6. NVENC este doar contract managed; implementarea nativă reală și sincronizarea suprafețelor nu sunt încă validate.
7. Transform Matrix3x2/CONTAIN trebuie verificat vizual pe landscape, portrait, rotație EXIF și WALL; compilarea nu dovedește coordonatele corecte.
8. RAW/EXIF/color management sunt încă incomplete; nu se declară Gate A final.
9. Device-removed/timeout diagnostics și recovery lipsesc încă.
10. Testele 10/50/120+ cu media reală nu se rulează până când traseul GPU + encoder nu este complet și diagnosticabil.

## Regula de ieșire
Nu se livrează installer de test utilizatorului până când: compile gate este verde; test nativ D3D12 automat produce și verifică pixeli; lifetime SRV/RTV este fence-safe; NVENC real funcționează; 10/50/120+ stress trece; apoi se validează pe RTX 4060 cu media reală.
