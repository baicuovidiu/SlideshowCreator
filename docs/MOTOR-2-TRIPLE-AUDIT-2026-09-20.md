# Motor 2.0 — TRIPLE AUDIT independent — 2026-09-20

Scop: trei audituri independente, cu metode diferite, de la fundația Motor 2.0 până la HEAD. Un rezultat comun nu este considerat rezolvat doar fiindcă apare într-un singur audit.

## Audit 1 — analiză statică transversală, contract → implementare
Metodă: citire directă a tuturor modulelor Motor2 relevante și urmărirea contractelor C# → ABI → C++ → D3D12 → encoder.

Erori/riscuri confirmate:
- NativeRenderTarget a fost refactorizat fără membrul rtvHeap, deși codul îl folosea: eroare de compilare reală.
- release_resource tratează orice pointer ca ComPtr<ID3D12Resource>*; un NativeRenderTarget are alt layout/ownership. Nu trebuie folosit generic pentru target.
- render target este creat pentru fiecare frame și nu are încă lifecycle/pool/release legat de consumul encoderului.
- SRV descriptors cresc monoton până la 4096; nu există reciclare fence-safe.
- srvByResource nu este curățat la release.
- context destroy nu face flush explicit al lucrului GPU în zbor.
- upload este blocant și creează allocator/list/upload heap per textură.
- begin_frame/end_frame nu delimitează efectiv command recording; draw_quads face aproape toată munca.
- un frame cu zero quads nu este curățat: managed nu cheamă DrawQuads când lista este goală, iar BeginFrame nu face clear.
- HardwareProfiler raportează D3D12=false/NVENC=false permanent; native probe nu este încă legat de HardwareCapabilities, deci calea reală nu poate trece corect preflight/init.
- NVENC este doar contract, fără sesiune nativă validată.
- telemetry Counter suprascrie valorile; hit/miss nu sunt contoare cumulative.
- RAW/EXIF/color management/VFR sunt incomplete, conform designului curent.

## Audit 2 — audit istoric/diferențial + CI
Metodă: reconstrucția succesiunii commiturilor și a Gate-urilor, apoi diff între ultimul punct verde înainte de audit și HEAD; verificare log CI exactă.

Rezultate:
- #94 ef3f39c991: SUCCESS.
- #95 1a8af48d80: SUCCESS.
- #96 5551e7522f: FAILURE.
- #97 cde05f3202: FAILURE.
- #98 64beb79ed7: FAILURE.
- Logul #98 localizează regresia la d3d12_device.cpp: NativeRenderTarget nu conține rtvHeap; erorile ulterioare sunt cascadă din aceeași lipsă.
- Diff-ul 1a8af48d80..HEAD confirmă că regresia a intrat exact în refactorul RTV per-target.
- Corecție aplicată: commit bbf19bdb62 adaugă rtvHeap în NativeRenderTarget și elimină membrul RTV global nefolosit din NativeContext.
- Gate #99 a fost pornit automat pentru această corecție; verdictul se ia numai după finalizarea CI.

## Audit 3 — audit adversarial pe invariants/lifetime/scenarii-limită
Metodă: presupunem că build-ul este verde și încercăm să invalidăm motorul prin scenarii care compilează, dar pot produce frame greșit, use-after-free, leak sau performanță falsă.

Erori/riscuri confirmate:
- CONVENȚIE TRANSFORM NEÎNCHISĂ: shaderul pornește de la quad în NDC [-1,1], în timp ce GateAStressScene folosește translații în pixeli, iar PhotoLayout produce transformări în pixeli. Fără conversie explicită pixel→NDC, obiectele pot ieși masiv din cadru.
- VRAM CACHE LIFETIME: GetOrUploadAsync poate declanșa TrimAsync în timp ce schedulerul încă construiește dicționarul de texturi pentru același frame. O textură deja selectată pentru frame poate deveni victimă și poate fi eliberată înainte de ComposeAsync: risc de use-after-free.
- FRAME TARGET LIFETIME: ComposeAsync returnează targetul către encoder, dar nu există ownership/fence care să spună când poate fi reciclat/eliberat.
- FRAMES-IN-FLIGHT este doar parțial: command allocators sunt triple-buffered, dar schedulerul managed rămâne secvențial decode→upload→compose→encode.
- GPU RESOURCE RELEASE nu este fence-aware.
- EMPTY FRAME: lipsa draw-urilor înseamnă lipsa clear-ului.
- DEVICE DESTROY: fără drain/flush explicit poate distruge resurse cu GPU work în zbor.
- COLOR: FP16 target există, dar RGBA8 SRV este UNORM, fără transfer sRGB/OCIO; nu se poate declara încă linear-light corect.
- TEST GAP: nu există încă un test runtime care citește/verifică pixeli produși de D3D12; compile gate nu poate detecta transform/blend/clear/color/lifetime bugs.

## Comparația celor trei audituri

Convergență puternică (apare prin cel puțin două metode):
1. Lifecycle GPU incomplet: target/SRV/release/fences.
2. Compile green nu dovedește runtime corect.
3. Pipeline-ul asincron este incomplet.
4. Traseul encoderului NVENC nu este încă validat.
5. Testele runtime/stress sunt insuficiente pentru a declara motorul funcțional.

Descoperiri unice importante:
- Audit 1: empty-frame clear, profiler neconectat, telemetry counters.
- Audit 2: localizarea exactă a regresiei RTV în commitul 5551e752 și confirmarea prin log CI.
- Audit 3: incompatibilitatea pixel-space vs NDC și posibilul use-after-free produs chiar de TrimAsync în timpul construirii unui frame.

## Ordine obligatorie de remediere
P0: build nativ verde după bbf19bdb62.
P0: unificare convenție transform și test numeric pixel→NDC.
P0: frame/resource ownership fence-safe; interzis release în timp ce frame-ul poate folosi textura.
P0: clear corect pentru frame gol + flush la destroy.
P1: descriptor allocator/recycling + curățare mapări.
P1: render-target pool cu 3+ targeturi și lifecycle compatibil NVENC.
P1: runtime D3D12 test cu readback doar în test, verificând pixeli/alpha/Z/contain.
P1: native NVENC + fence interoperability.
P1: stress 10/50/120+.
P2: pipeline managed asincron, upload pool, telemetry cumulativă.
P2: RAW/EXIF/OCIO/ACES/VFR complet.

## Regula Triple Audit
Înaintea unui installer pentru utilizator, aceeași versiune trebuie să treacă: (A) audit static de contracte, (B) audit CI/diff/istoric, (C) audit adversarial runtime/invariants. Orice FAIL sau P0 deschis blochează livrarea.
