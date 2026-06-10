# PROGRESS — Project: Greenkeeper

> Blok ilerleme kaydı. Bir blok kapandığında buraya tarih + kısa not düşülür.
> Blok tanımları: TDD §12. Kapanış kriteri: çalışan tanım + test + debug görünümü.

## Aktif

- **Faz 0 / F0.2** — PitchGrid (CPU) + tick iskeleti + F1 overlay *(başlamadı)*

## Tamamlanan Bloklar

| Blok | Kapanış Tarihi | Not |
|---|---|---|
| F0.1 | 2026-06-10 | Klasör/asmdef zinciri (Core←Pitch←Tools←Gameplay←UI), Input Actions, Boot→Stadium akışı, FP kontrolcü (CharacterController + Cinemachine FP rig), PerfHud (F2). Test: AsmdefDependencyTests 2/2 yeşil. Cinemachine 3.1.4 eklendi. Not: .inputactions C# wrapper codegen batchmode'da tavuk-yumurta sorunu verdiği için InputActionAsset isimle çözülüyor (generateWrapperCode kapalı). |

## Faz 0 Kontrol Listesi

- [x] F0.1 — Proje kurulumu (Unity 6 + URP, klasör/asmdef, Input, Boot→Stadium) ✅ 2026-06-10
- [ ] F0.2 — PitchGrid (CPU) + tick iskeleti + F1 overlay
- [ ] F0.3 — RT seti + fırça stamp altyapısı
- [ ] F0.4 — Çim render (instanced blade + LOD) — ⚠ T1 performans ölçümü burada
- [ ] F0.5 — MowerTool (biçme hissi)
- [ ] F0.6 — Desen / şerit yansıması
- [ ] F0.7 — His turu + **dış test: "bırakamadım" kriteri**
