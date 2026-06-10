# PROGRESS — Project: Greenkeeper

> Blok ilerleme kaydı. Bir blok kapandığında buraya tarih + kısa not düşülür.
> Blok tanımları: TDD §12. Kapanış kriteri: çalışan tanım + test + debug görünümü.

## Aktif

- **Faz 0 / F0.3** — RT seti + fırça stamp altyapısı *(başlamadı)*

## Tamamlanan Bloklar

| Blok | Kapanış Tarihi | Not |
|---|---|---|
| F0.1 | 2026-06-10 | Klasör/asmdef zinciri (Core←Pitch←Tools←Gameplay←UI), Input Actions, Boot→Stadium akışı, FP kontrolcü (CharacterController + Cinemachine FP rig), PerfHud (F2). Test: AsmdefDependencyTests 2/2 yeşil. Cinemachine 3.1.4 eklendi. Not: .inputactions C# wrapper codegen batchmode'da tavuk-yumurta sorunu verdiği için InputActionAsset isimle çözülüyor (generateWrapperCode kapalı). |
| F0.2 | 2026-06-10 | CellState (≤16 B, test korumalı) + PitchGrid (212×140, world↔cell eşleme) + deterministik init + 4 Hz tick (düz C#; Burst, Poyraz kararıyla ertelendi — TDD T3 fallback) + F1 katman overlay'i (nem/sağlık/yükseklik/hasar/desen) + StadiumDefinitionSO (Data/Stadium_Default.asset). Testler 19/19 yeşil. ⚠ Tick 0.5–0.7 ms — bütçenin (0.5 ms) hafif üstünde, bloklayıcı değil (Poyraz onayı); bilinen çözüm: PitchTick.Step'i Burst IJobParallelFor ile sarmalamak. |

## Faz 0 Kontrol Listesi

- [x] F0.1 — Proje kurulumu (Unity 6 + URP, klasör/asmdef, Input, Boot→Stadium) ✅ 2026-06-10
- [x] F0.2 — PitchGrid (CPU) + tick iskeleti + F1 overlay ✅ 2026-06-10 (⚠ tick 0.5–0.7 ms, bütçe aşımı kabul edildi)
- [ ] F0.3 — RT seti + fırça stamp altyapısı
- [ ] F0.4 — Çim render (instanced blade + LOD) — ⚠ T1 performans ölçümü burada
- [ ] F0.5 — MowerTool (biçme hissi)
- [ ] F0.6 — Desen / şerit yansıması
- [ ] F0.7 — His turu + **dış test: "bırakamadım" kriteri**
