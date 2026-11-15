# ART DIRECTION — Project: Greenkeeper

> Görsel yön dokümanı. Modeller, materyaller ve arayüz bu belgeye uyar.
> Bağlı dokümanlar: GDD v1.2 (P1 "Görsel Tatmin"), TDD v1.1, `ASSET_LIST.md`.
> Sürüm: v1.0 — 15 Kasım 2026

---

## 1. Tek cümlelik stil

**Temiz, stilize-gerçekçi:** gerçek dünya oranları ve gerçek nesneler, ama sade formlar, az detay, yumuşak ve doygun renkler. Referans his: *PowerWash Simulator*'ın okunaklılığı + *Lawn Mowing Simulator*'ın nesneleri, daha sade ve daha sıcak.

**Neden bu stil:**
- Oyunun yıldızı çim, şerit deseni ve çizgilerdir (procedural, shader'da). Modeller bunlarla yarışmamalı, onları çerçevelemeli.
- Sade formlar AI üretiminin kusurlarını (bozuk topoloji, doku dikişleri) gizler.
- Tek kişilik projede tutarlılık, detaydan daha önemlidir.

## 2. Kurallar

1. **Ölçek gerçektir.** Biçme makinesi gerçek bir biçme makinesi boyutundadır. Saha FIFA ölçüsündedir (105×68 m). Ölçek bozulursa oyuncu bunu hemen hisseder.
2. **Siluet önce gelir.** Her nesne 20 m'den tanınmalıdır. Küçük detay (vida, yazı, kablo) eklenmez.
3. **Yüzey sadedir.** Düz renk bölgeleri + hafif ton geçişi. Yoğun doku, yazı, logo, aşırı kir/pas yok.
4. **Hafif kullanılmışlık.** Aletler yeni değil ama bakımlı: kenarlarda hafif aşınma, tekerlekte hafif çamur. Hurda görünümü yok.
5. **Marka yok.** Gerçek marka, logo veya ürün adı kullanılmaz (lisans ve özgünlük).

## 3. Renk paleti

### Saha (shader'dan gelir, modeller bunlara uyum sağlar)
| Kullanım | Ton |
|---|---|
| Sağlıklı çim | Canlı, sıcak yeşil |
| Kuru çim | Hardal sarısı |
| Islak çim | Koyu, doygun yeşil |
| Saha boyası | Saf beyaz |
| Çizgi rehberi | Açık camgöbeği (yalnız rehber için ayrılmıştır, başka yerde kullanılmaz) |

### Ekipman
| Kullanım | Ton | Not |
|---|---|---|
| Ana gövde | Koyu orman yeşili | Çimden koyu, sahada kaybolmaz |
| Vurgu | Turuncu | Tutamaç, kapak, uyarı parçaları; oyuncunun gözü buraya gitsin |
| Metal | Orta gri, hafif parlak | Şasi, bıçak muhafazası |
| Lastik / plastik | Koyu antrasit | Siyah değil (siyah düz okunur) |
| Krem | Kırık beyaz | Etiket alanları, ince şeritler |

### Stadyum çevresi
| Kullanım | Ton |
|---|---|
| Beton / tribün | Açık sıcak gri |
| Koltuklar | Kulüp rengi (kademe başına değişebilir; Kademe 1 için soluk mavi) |
| Reklam panoları | Düz renk blokları, sahte ve jenerik içerik |
| Kale direkleri, korner bayrağı direği | Beyaz |
| Korner bayrağı | Sarı veya turuncu |

## 4. Teknik standart

### Poligon bütçesi (üçgen)
| Sınıf | Örnek | Bütçe |
|---|---|---|
| Elde tutulan / birinci şahıs görünen alet | Biçme makinesi, çizgi arabası, hortum | 3.000–8.000 |
| Küçük sahne nesnesi | Korner bayrağı, divot çatalı, kova | 300–1.500 |
| Orta yapı | Kale, kulübe, pano | 1.000–5.000 |
| Tekrarlanan modül | Tribün bloğu, çit parçası | ≤ 2.000 (birçok kopya olur) |

Çim bütçesi (≤ 4 ms GPU) oyunun en pahalı kalemi; modeller bu yüzden mütevazı kalmalı.

### Doku
- Varsayılan: **tek albedo (base color) doku, 1024²**. Alet başına en fazla 2048².
- Normal map yalnız gerekiyorsa. Metallic/roughness Unity materyalinden sabit değerle verilir.
- Tek materyal tercih edilir; en fazla 2 materyal.

### Unity import standardı
- **Birim:** 1 Unity birimi = 1 metre. Import'ta Scale Factor 1 olacak şekilde Blender'da düzeltilir.
- **Pivot:** zemine değen noktanın ortası (alt-merkez). Kapı ve kapak gibi dönen parçalarda menteşe.
- **Yön:** ön yüz +Z.
- **Dönüşümler uygulanmış** (Blender'da Apply All Transforms).
- **Format:** FBX (veya GLB). Animasyon yok, rig yok.
- **Materyal:** URP Lit, `Assets/_Project/Art/Materials/` altında; model içine gömülü materyal kullanılmaz.
- **Collider:** basit primitif (box/capsule); mesh collider yalnız gerekiyorsa.
- **İsimlendirme:** `SM_<Ad>` (mesh), `M_<Ad>` (materyal), `T_<Ad>_Albedo` (doku), `PF_<Ad>` (prefab).
- **Klasör:** `Assets/_Project/Art/Models/<Kategori>/`.

## 5. Üretim yolu

| Nesne türü | Yol | Neden |
|---|---|---|
| Karmaşık, organik formlu alet | **Tripo** → Blender temizlik → Unity | AI burada en çok zaman kazandırır |
| Basit geometrik yapı (kale, direk, pano, çit, tribün modülü) | **Kodla / Blender Python ile** | AI basit geometride bile bozuk topoloji üretir; silindir ve kutuyu kodla yapmak hem daha temiz hem daha hızlı |
| Ses | CC0 kütüphaneler (Freesound vb.) | Prosedürel sesler yer tutucu |

### Tripo → Unity kontrol listesi
1. Prompt şablonuyla üret (bölüm 6). 3–4 varyant al, en temiz silueti seç.
2. Blender'da: ölçeği gerçek boyuta getir, pivotu alt-merkeze taşı, Apply All Transforms.
3. Poligon bütçesini aşıyorsa Decimate.
4. Ayrık parçaları ve görünmeyen iç yüzleri temizle.
5. FBX olarak `Assets/_Project/Art/Models/<Kategori>/` altına koy.
6. Claude Code'a import, materyal ve prefab işini ver; bu belgeye uymasını iste.
7. Oyunda göz kontrolü: ölçek doğru mu, renk palete uyuyor mu, 20 m'den tanınıyor mu?

> Tripo'nun kullanım koşullarını (özellikle ücretsiz planda üretilen modellerin kullanım hakları) kendi planın için kontrol et. Proje ticari değil ama GitHub'da açık.

## 6. Tripo prompt şablonu

Tüm modellerde aynı şablon kullanılır; stil tutarlılığı buradan gelir.

```text
Create a 3D model of [NESNE].
Style: clean stylized-realistic game asset, simple readable forms, soft saturated colors, low detail, no text, no logos, no brand names.
Shape details: [SİLUET, ORANLAR, ANA PARÇALAR].
Colors: dark forest green main body, orange accents on [PARÇA], medium grey metal, dark charcoal rubber.
Materials: painted metal, rubber, plastic. Lightly used but well maintained.
Use case: first-person simulation game prop, real-world scale.
Output: clean geometry, readable silhouette, single object, no ground plane, neutral pose.
```

Mümkünse **image-to-3D** text-to-3D'den daha tutarlı sonuç verir: önce bir görsel üretici ile aynı stilde ön görünüm görseli üretip Tripo'ya o görseli vermek, tüm setin aynı elden çıkmış gibi görünmesini kolaylaştırır.

## 7. Arayüz (UI) dili

Ayrıntılı tasarım Figma'da yapılacak; bu bölüm ortak zemin.

- **Panel:** yarı saydam koyu orman yeşili, yuvarlatılmış köşe (8 px).
- **Metin:** kırık beyaz; vurgu turuncu (ekipman vurgusuyla aynı).
- **Durum renkleri:** iyi = yeşil, uyarı = hardal, kötü = kırmızımsı turuncu. Karne notları (A+…F) bu skaladan renk alır.
- **Yazı tipi:** tek, okunaklı bir sans-serif (karar Figma aşamasında).
- **Ton:** spor yayını grafiği + iş tableti. Ciddi ama sıcak; parodi değil.
