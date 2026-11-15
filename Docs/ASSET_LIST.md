# ASSET LIST — Project: Greenkeeper

> Üretim takip listesi. Stil ve teknik kurallar: `ART_DIRECTION.md`.
> Durum: Bekliyor → Üretimde → Temizlik → Oyunda ✅
> Kapsam: bitirme teslimi (4 Ocak 2027). Toplam hedef 10–12 ana model.

---

## Öncelik 1 — Aletler (Hafta 2: 16–22 Kasım)

Oyuncunun sürekli birinci şahıs gördüğü nesneler; görsel kalitenin en çok hissedildiği yer.

| # | Nesne | Yol | Bütçe | Not | Durum |
|---|---|---|---|---|---|
| 1 | İtme çim biçme makinesi | Tripo | 3–8k | Mevcut kutu+silindir yer tutucunun yerine. Arka toplama sepeti görünür olmalı (sepet mekaniği). Sepet kapağı prefab'da (BagLid, M_Tire). Model ofseti 0,45, FOV 60 ile tutamak ~20° aşağı bakışta görünür. | Oyunda ✅ |
| 2 | Çizgi arabası (line marker) | Tripo | 3–8k | Tek tekerli veya iki tekerli boya arabası, üstte boya tankı. | Bekliyor |
| 3 | Hortum + tabanca ucu | Tripo | 2–5k | Birinci şahısta yalnız tabanca ucu görünür; hortum gövdesi kısa bir parça. | Bekliyor |
| 4 | Hortum makarası | Tripo | 2–4k | Saha kenarında sabit sahne nesnesi. | Bekliyor |

## Öncelik 2 — Saha nesneleri (Hafta 2–3)

Basit geometri; kodla veya Blender Python ile üretilir.

| # | Nesne | Yol | Bütçe | Not | Durum |
|---|---|---|---|---|---|
| 5 | Kale (direkler + ağ) | Kod / Blender Python | 1–3k | FIFA: 7,32 × 2,44 m, direk çapı ≤ 12 cm. Ağ yarı saydam doku. | Bekliyor |
| 6 | Korner bayrağı | Kod / Blender Python | 300–800 | Direk ≥ 1,5 m, bayrak hafif sallanabilir (shader). | Bekliyor |
| 7 | Divot çatalı / onarım aleti | Tripo | 500–1,5k | M.6 onarım bloğu için. | Bekliyor |

## Öncelik 3 — Stadyum çevresi (Hafta 4–5)

Kademe 1–2 stadyumu: küçük, sade, sahayı çerçeveleyen bir çevre. Tekrarlanan modüllerle kurulur.

| # | Nesne | Yol | Bütçe | Not | Durum |
|---|---|---|---|---|---|
| 8 | Tribün modülü | Kod / Blender Python | ≤ 2k | Birkaç sıra koltuk + beton basamak; uç uca dizilir. | Bekliyor |
| 9 | Reklam panosu | Kod / Blender Python | ≤ 500 | Düz kutu; jenerik, sahte içerikli doku. | Bekliyor |
| 10 | Çevre çiti / bariyer | Kod / Blender Python | ≤ 500 | Tekrarlanan modül. | Bekliyor |
| 11 | Saha görevlisi kulübesi | Tripo veya kod | 2–5k | "Günü Bitir" etkileşiminin diegetik yeri (IDEAS: yatak/ofis). | Bekliyor |
| 12 | Işık direği | Kod / Blender Python | 1–2k | Siluet ve ufuk çizgisi için; işlevsel ışık şart değil. | Bekliyor |

## Ses (Hafta 5)

| # | Ses | Kaynak | Not | Durum |
|---|---|---|---|---|
| S1 | Biçme makinesi motor döngüsü | CC0 | Prosedürel motorun yerine | Bekliyor |
| S2 | Çim kesim hışırtısı | CC0 | Prosedürel hışırtının yerine | Bekliyor |
| S3 | Hortum spreyi | CC0 | IDEAS: hortum sesi (M.9) | Bekliyor |
| S4 | Çizgi arabası tekerlek / boya | CC0 | | Bekliyor |
| S5 | Stadyum ambiyansı | CC0 | Kuş, uzak şehir; maç günü kalabalık uğultusu | Bekliyor |

## Kapsam dışı (geri ekleme listesi)

- Denetçi karakteri (karakter modeli + animasyon gerektirir)
- Binilen biçme makinesi (Faz 2)
- Kademe 3+ stadyumlar
- Sprinkler sistemi (Faz 2)

## Üretim kayıtları

Her model için kısa not: kullanılan prompt, kaç varyant denendi, Blender'da ne düzeltildi. Rapor yazarken işe yarar.

| Tarih | Nesne | Not |
|---|---|---|
| 2026-11-15 | İtme çim biçme makinesi | Tripo Akıllı Ağ P2.0, 4 varyant; AI doku 2. denemede; tekerlekler Blender script'iyle silindire çevrildi. 6.100 üçgen, 2048² albedo, 2 materyal (M_PushMower, M_Tire). Tripo dokusu JPEG, .jpg olarak tutuldu. |
