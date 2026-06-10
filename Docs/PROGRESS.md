# PROGRESS — Project: Greenkeeper

> Blok ilerleme kaydı. Bir blok kapandığında buraya tarih + kısa not düşülür.
> Blok tanımları: TDD §12. Kapanış kriteri: çalışan tanım + test + debug görünümü.

## Aktif

- **Faz 0 / F0.5** — MowerTool: kapsül süpürme, çift-katman yazım, kesim yüksekliği ayarı, sepet *(başlamadı)*

## Tamamlanan Bloklar

| Blok | Kapanış Tarihi | Not                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| ---- | -------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| F0.1 | 2026-06-10     | Klasör/asmdef zinciri (Core←Pitch←Tools←Gameplay←UI), Input Actions, Boot→Stadium akışı, FP kontrolcü (CharacterController + Cinemachine FP rig), PerfHud (F2). Test: AsmdefDependencyTests 2/2 yeşil. Cinemachine 3.1.4 eklendi. Not: .inputactions C# wrapper codegen batchmode'da tavuk-yumurta sorunu verdiği için InputActionAsset isimle çözülüyor (generateWrapperCode kapalı).                                                                                 |
| F0.2 | 2026-06-10     | CellState (≤16 B, test korumalı) + PitchGrid (212×140, world↔cell eşleme) + deterministik init + 4 Hz tick (düz C#; Burst, Poyraz kararıyla ertelendi — TDD T3 fallback) + F1 katman overlay'i (nem/sağlık/yükseklik/hasar/desen) + StadiumDefinitionSO (Data/Stadium_Default.asset). Testler 19/19 yeşil. ⚠ Tick 0.5–0.7 ms — bütçenin (0.5 ms) hafif üstünde, bloklayıcı değil (Poyraz onayı); bilinen çözüm: PitchTick.Step'i Burst IJobParallelFor ile sarmalamak. |
| F0.3 | 2026-06-10     | PitchBounds (world↔UV sabit eşleme, shader UvTransform) + RT seti (RT_GrassState RGBA16F 2048², RT_Pattern RG16F 2048², RT_Paint R8 4096²) + CommandBuffer quad-stamp altyapısı (Blit değil, LateUpdate single-CB) + PitchBrush.shader (kamerasız UV→NDC, radyal falloff, platform Y-flip korumalı) + RtDebugViewer (F4) + DebugTestBrush (LMB+B; ⚠ geçici — F0.5 MowerTool gelince kaldırılması değerlendirilir) + PerfHud stamp sayacı. Testler 28/28 yeşil (3 GPU smoke test dahil: merkez iz, off-center Y-flip, soft falloff). Editör turu Poyraz tarafından doğrulandı. |
| F0.4 | 2026-06-11     | Instanced çim render (TDD §3.3): GrassTileLayout (8×8 m, 14×9=126 tile, frustum cull) + GrassLod (3 bant, parçalı-doğrusal yoğunluk, orta bantta geniş blade) + GrassRenderer (`RenderMeshPrimitives`, blade pozisyonu CPU buffer'sız instanceID hash'inden, gölgesiz) + GrassBlade.shader (RT_GrassState örnekler: yükseklik→scale, sağlık/nem→renk, hasar→cull; sway; per-vertex half-Lambert) + PitchGround.shader (uzak LOD: zemin aynı RT'den tint okur) + GrassRenderSettingsSO (tuning verisi SO'da) + RT_GrassState kanal semantiği sabitlendi (R yükseklik, G sağlık, B nem, A hasar; GrassStateEncoding test korumalı) + grid→RT başlangıç tohumlama bliti. Debug: F2'de GPU ms (FrameTimingManager) + tile/instance/cull; G çim aç/kapat (A/B ölçüm); F5 LOD bant tinti. Testler 43/43 yeşil. **T1 GEÇTİ:** çim maliyeti saha ortasında ~0.34 ms, köşede ~0.26 ms GPU — bütçe ≤4 ms, rahat içinde. ⚠ Ölçüm GPU'su RTX 3070 Laptop; TDD referansı (GTX 1660) daha zayıf — referans donanımda pay hâlâ geniş ama F0.7 his turunda yoğunluk artarsa yeniden ölçülmeli. Desen yatışı (RT_Pattern bend) bilinçli olarak F0.6'da. |

## Faz 0 Kontrol Listesi

- [x] F0.1 — Proje kurulumu (Unity 6 + URP, klasör/asmdef, Input, Boot→Stadium) ✅ 2026-06-10
- [x] F0.2 — PitchGrid (CPU) + tick iskeleti + F1 overlay ✅ 2026-06-10 (⚠ tick 0.5–0.7 ms, bütçe aşımı kabul edildi)
- [x] F0.3 — RT seti + fırça stamp altyapısı ✅ 2026-06-10
- [x] F0.4 — Çim render (instanced blade + LOD) ✅ 2026-06-11 (T1 geçti: ~0.34 ms GPU @ RTX 3070 Laptop)
- [ ] F0.5 — MowerTool (biçme hissi)
- [ ] F0.6 — Desen / şerit yansıması
- [ ] F0.7 — His turu + **dış test: "bırakamadım" kriteri**
