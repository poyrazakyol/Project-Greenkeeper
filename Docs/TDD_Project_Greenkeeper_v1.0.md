# TECHNICAL DESIGN DOCUMENT
## PROJECT: GREENKEEPER *(Çalışma Adı)*
### Stadyum Çim Müdürlüğü Simülasyonu — Teknik Tasarım

| | |
|---|---|
| **Doküman Sürümü** | v1.0 |
| **Tarih** | 9 Haziran 2026 |
| **Bağlı Doküman** | GDD v1.1 (tasarım otoritesi) |
| **Geliştirici** | Poyraz — solo proje |
| **Motor** | Unity 6 LTS (6000.x) — URP |
| **Hedef Platform** | PC (Windows öncelikli), Steam |
| **Dil / Araçlar** | C#, HLSL (Shader Graph + custom shader), Claude Code |

> **Doküman Amacı:** Bu TDD, GDD v1.1'de tanımlanan sistemlerin teknik uygulamasını tanımlar. Claude Code ile geliştirme sürecinde mimari otorite bu dokümandır. Çelişki durumunda tasarım sorularında GDD, teknik sorularda TDD geçerlidir.

> **Kapsam Disiplini (R3):** Bu dokümandaki her sistem, GDD'deki faz planına bağlıdır. Faz 0 ve MVP kapsamı dışındaki sistemlerin mimarisi burada tanımlanır ancak **uygulaması MVP kapanmadan başlatılmaz.** Claude Code'a verilen görevler faz etiketi taşımalıdır.

---

## 1. TEKNİK GENEL BAKIŞ

### 1.1 Teknoloji Yığını

| Katman | Seçim | Gerekçe |
|---|---|---|
| Motor | Unity 6 LTS | Solo geliştirici için LTS istikrarı; URP olgunluğu; GPU instancing/BatchRendererGroup desteği |
| Render Pipeline | URP | Hedef görsel stil (temiz, stilize-gerçekçi) için yeterli; Forward+ ile çok ışık ihtiyacı düşük; custom shader entegrasyonu kolay |
| Input | Input System (yeni) | Cihaz bağımsızlık, action map'ler (FP kontrol / araç kontrol / menü ayrımı) |
| Kamera | Cinemachine 3.x | FP rig + araç kamerası + drone modu geçişleri |
| UI | UI Toolkit (ekranlar) + World-space uGUI (diegetik öğeler) | Karne/menü gibi ekran UI'ları UI Toolkit ile hızlı iterasyon |
| Kayıt | JSON (durum) + binary blob (saha haritaları) | İnsan-okunur durum + kompakt harita verisi (bkz. §9) |
| Versiyon Kontrol | Git + Git LFS (texture/model) | Claude Code iş akışıyla doğal entegrasyon |

### 1.2 Mimari Felsefe

1. **ScriptableObject + Event Channel mimarisi:** Sistemler birbirini doğrudan referans almaz; SO tabanlı event kanalları üzerinden haberleşir. Veri (ekipman statları, lig standartları, sözleşmeler) SO asset'leridir — kod değişmeden içerik eklenebilir.
2. **CPU simülasyon / GPU görselleştirme ayrımı:** Oyun mantığı (sağlık, nem, puanlama) CPU tarafında deterministik bir grid üzerinde yaşar; yüksek çözünürlüklü görsel detay (biçme izi, boya, desen) GPU RenderTexture'larında yaşar. İkisi senkronize edilir ama otorite ayrımı nettir (bkz. §3).
3. **Tek sahne, veri ile çeşitlilik:** Stadyumlar ayrı sahneler değil; tek "Stadium" sahnesi + `StadiumDefinitionSO` ile yüklenen çevre/parametre setidir (MVP'de tek stadyum olduğu için maliyetsiz, ileride ölçeklenir).
4. **Her sistemin debug görünümü vardır:** Görselleştirilemeyen sistem doğrulanamaz. Durum haritası katmanları (nem, sağlık, yükseklik...) F-tuşlarıyla overlay olarak izlenebilir olmalıdır (bkz. §11).

### 1.3 Performans Bütçeleri (Hedefler)

| Metrik | Hedef | Not |
|---|---|---|
| Frame rate | 60 FPS @ 1080p, orta sınıf GPU (GTX 1660 / RX 580 sınıfı) | Çim sistemi bütçenin en büyük kalemi |
| Çim render | ≤ 4 ms GPU | Instancing + LOD + mesafe yoğunluk azaltma |
| Durum simülasyonu (gün içi tick) | ≤ 0.5 ms CPU / tick | Tick 4 Hz'de çalışır, frame başına değil (bkz. §3.4) |
| Fırça yazımı (biçme/boya) | ≤ 0.3 ms GPU / stamp | CommandBuffer ile birleştirilmiş blit |
| Kayıt dosyası boyutu | ≤ 5 MB / slot | Harita blob'ları RLE sıkıştırmalı |
| Sahne yükleme | ≤ 10 sn | Tek stadyum sahnesi, addressable gerekmiyor (MVP) |

---

## 2. PROJE YAPISI

### 2.1 Klasör Yapısı

```
Assets/
├── _Project/                      # Tüm proje varlıkları (3rd party ile karışmaz)
│   ├── Scripts/
│   │   ├── Core/                  # GameManager, TimeSystem, SaveSystem, bootstrap
│   │   ├── Events/                # Event channel SO tanımları + listener'lar
│   │   ├── Pitch/                 # PitchGrid, PitchSimulation, PitchRenderBridge
│   │   ├── Tools/                 # ITool, MowerTool, HoseTool, LinerTool, RepairTool
│   │   ├── Player/                # FP controller, etkileşim, alet çarkı
│   │   ├── Inspection/            # Denetçi sistemi, puanlama, karne
│   │   ├── Economy/               # Para, itibar, sözleşmeler
│   │   ├── Match/                 # Maç günü akışı, hasar üretimi
│   │   ├── Weather/               # (Faz 2) Hava durumu servisi
│   │   ├── UI/                    # Ekran ve diegetik UI controller'ları
│   │   └── Debug/                 # Overlay, cheat konsolu, harita görüntüleyici
│   ├── Data/                      # SO asset'leri (ekipman, ligler, sözleşmeler...)
│   ├── Art/                       # Modeller, materyaller, texture'lar
│   ├── Shaders/                   # Çim, zemin, fırça shader'ları
│   ├── Prefabs/
│   ├── Scenes/                    # Boot, MainMenu, Stadium
│   └── Settings/                  # URP asset'leri, Input Actions
└── Plugins/                       # 3rd party
```

### 2.2 Assembly Definitions

Derleme süresi ve bağımlılık disiplini için asmdef ayrımı:

```
Greenkeeper.Core ← (hiçbir şeye bağımlı değil: zaman, event, save arayüzleri)
Greenkeeper.Pitch ← Core
Greenkeeper.Tools ← Core, Pitch
Greenkeeper.Gameplay ← Core, Pitch, Tools   (player, inspection, economy, match)
Greenkeeper.UI ← Core, Gameplay
Greenkeeper.Debug ← hepsi (yalnızca editor/dev build)
```

**Kural:** Bağımlılık oku tek yönlüdür. `Pitch`, `Tools`'u bilmez; `Tools` event kanalı üzerinden Pitch'e yazar. Bu, Claude Code'un bir sistemde çalışırken diğerini kırmasını yapısal olarak engeller.

### 2.3 Sahne Yapısı

| Sahne | İçerik |
|---|---|
| `Boot` | Bootstrap: kalıcı sistemler (GameManager, SaveSystem, EventSystem), ardından MainMenu yükler |
| `MainMenu` | Ana menü, slot seçimi |
| `Stadium` | Oyunun tamamı. `StadiumDefinitionSO`'ya göre çevre prefab'ı + saha parametreleri yüklenir |

---

## 3. SAHA DURUM SİSTEMİ (PITCH STATE) — *Teknik Bel Kemiği*

> GDD §3.0'daki durum modelinin uygulamasıdır. **R1 riskinin cevabı bu bölümdür.**

### 3.1 İki Katmanlı Model: CPU Grid + GPU Detay

**Problem:** Oyun mantığı (denetçi puanlama, sağlık simülasyonu, kayıt) deterministik ve sorgulanabilir veri ister; görsel tatmin (biçme izinin keskin kenarı, boya detayı) yüksek çözünürlük ister. Tek yapıda ikisini birden çözmek ya pahalı (yüksek çözünürlüklü CPU grid) ya güvenilmezdir (GPU readback'e bağımlı mantık).

**Çözüm:** Otorite ayrımı.

| Katman | Yapı | Çözünürlük | Otorite Olduğu Veri |
|---|---|---|---|
| **CPU Grid** (`PitchGrid`) | `NativeArray<CellState>` | Hücre = 0.5 m → 105×68 m saha için **212×140 ≈ 29.7k hücre** | Nem, sağlık, sıkışma, hasar, *mantıksal* çim yüksekliği, büyüme — tüm simülasyon ve puanlama |
| **GPU Detay** (RenderTexture seti) | RT'ler, saha alanına UV-eşlenmiş | 2048×2048 (MVP; saha üzerinde ~5 cm/texel) | Biçme izi keskinliği, desen yönü, boya, hasar dekalleri — yalnızca *görsel* |

**Senkronizasyon kuralları:**
- Aletler **her iki katmana birden yazar**: biçme stroke'u CPU grid'de yükseklik değerini günceller *ve* GPU height-detail RT'sine fırça basar. Tek kaynaktan (stroke verisi) çift yazım — readback gerekmez.
- GPU→CPU readback **yalnızca** çizgi düzgünlük skoru gibi piksel-hassas değerlendirmelerde, `AsyncGPUReadback` ile ve yalnızca denetim anında yapılır (frame-kritik değil).
- Gün-sonu simülasyonu yalnızca CPU grid üzerinde koşar; görsel RT'ler sabahında CPU grid'den toplu güncellenir (tek fullscreen blit, `PitchRenderBridge`).

```csharp
// Pitch/CellState.cs — 16 byte/hücre hedefi (29.7k hücre ≈ 475 KB)
public struct CellState {
    public half GrassHeightMm;   // 0–60 mm
    public byte Health;          // 0–100
    public byte Moisture;        // 0–100
    public byte Compaction;      // 0–100 (MVP'de pasif/sabit)
    public byte Damage;          // 0–100
    public byte PaintCoverage;   // 0–100 (mantıksal; görsel detay RT'de)
    public byte ZoneId;          // bölge etiketi (kale sahası, orta saha... hasar dağılımı için)
    public half PatternDirX;     // biçme yön vektörü (estetik puanlama)
    public half PatternDirY;
    public byte _reserved;
}
```

### 3.2 GPU RenderTexture Seti

| RT | Format | Kanallar | Yazan |
|---|---|---|---|
| `RT_GrassState` | RGBA16F, 2048² | R: yükseklik detayı, G: sağlık tint, B: nem tint, A: hasar maskesi | Alet fırçaları + gün-sonu bridge |
| `RT_Pattern` | RG16F, 2048² | RG: biçme yön vektörü (şerit yansıması için) | Biçme aleti |
| `RT_Paint` | R8, 4096² *(boya keskinlik ister)* | R: boya opaklığı | Çizgi aleti, solma blit'i |

- Fırça yazımı: `CommandBuffer` + fırça materyali (`Blit` değil, quad-stamp; pozisyon/rotasyon/yarıçap parametreli). Frame başına stamp'ler tek CommandBuffer'da birleştirilir.
- RT'ler `Camera` bağımsızdır; saha world-space → UV eşlemesi sabit matristir (`PitchBounds`).

### 3.3 Çim Render Yaklaşımı

**Seçilen teknik: GPU instanced grass blade + mesafe bazlı yoğunluk LOD.** (Shell texturing yedek plan olarak değerlendirildi; blade instancing desen/yükseklik kontrolünde daha doğrudan olduğu için tercih edildi.)

- Saha, **çim tile'larına** bölünür (örn. 8×8 m, ~117 tile). Her tile bir instanced batch'tir; frustum culling tile bazında CPU'da yapılır.
- Blade'ler vertex shader'da `RT_GrassState` + `RT_Pattern` örnekler:
  - Yükseklik → blade scale (biçilmiş bölge anında kısalır — P1).
  - Desen yönü → blade yatış (bend) yönü; normal bu yöne bükülür → **şerit parlaklık kontrastı bedavaya gelir** (specular/diffuse açı farkı). Oyunun imza görseli budur.
  - Sağlık/nem → renk gradyanı (canlı yeşil ↔ sarı ↔ kahve).
  - Hasar maskesi → blade discard (kel nokta) + zemin texture'unda çamur dekalı.
- LOD: yakın mesafe tam yoğunluk (~900 blade/m²'ye kadar değil! — hedef ~250 blade/m² yakında), orta mesafe yarı yoğunluk + geniş blade, uzak mesafe blade yok → zemin texture'u aynı RT'leri örnekleyerek renk/şerit gösterir (uzaktan şerit zaten texture olarak okunur, blade gerekmez).
- **Faz 0 doğrulama hedefi:** Bu yaklaşım, performans bütçesi (≤4 ms) ve "biçme hissi" açısından Faz 0'da doğrulanır. Başarısız olursa yedek plan: zemin-texture-ağırlıklı çözüm (blade yalnızca 10 m yakında).

### 3.4 Simülasyon Tick Mimarisi

İki tempolu simülasyon (GDD §2.4 zaman modeliyle uyumlu):

| Tick | Sıklık | İşler |
|---|---|---|
| **Gün-içi tick** | 4 Hz (0.25 sn) | Nem buharlaşması (yavaş), sulama yayılımı, görsel tint güncelleme ihtiyacı işaretleme |
| **Gün-sonu simülasyonu** | "Günü Bitir" anında, tek seferde | Çim büyümesi (büyüme hızı = f(sağlık, nem, mevsim, gübre)), gübre etki ilerlemesi (Faz 2), tohum filizlenme sayaçları, boya solması, nem gece dengelenmesi, hasar→sağlık erozyonu (ihmal birikimi) |

- Gün-içi tick `IJobParallelFor` (Burst) ile 29.7k hücrede koşar — bütçe içinde rahat.
- Gün-sonu simülasyonu sonuçları `DayEndedEvent` üzerinden yayınlanır; `PitchRenderBridge` RT'leri toplu günceller, UI özet kartı gösterir ("Gece: çim +1.2 mm uzadı, 3 bölge kurudu").
- **Determinizm:** Gün-sonu hesabı seed'li ve saf fonksiyondur (aynı grid + aynı gün = aynı sonuç) → kayıt/yükleme tutarlılığı ve test edilebilirlik.

---

## 4. ALET SİSTEMİ (TOOLS)

### 4.1 Mimari

```csharp
public interface ITool {
    ToolDefinitionSO Definition { get; }
    void OnEquip(ToolContext ctx);
    void OnUseHeld(ToolContext ctx, float dt);   // tetik basılı
    void OnUseReleased(ToolContext ctx);
    void OnUnequip();
}
```

- `ToolContext`: oyuncu pozisyonu, bakış ray'i, `PitchGrid` yazım API'si, GPU stamp kuyruğu, input durumu.
- Her alet bir `ToolDefinitionSO`'dan statlarını okur (genişlik, hız, kapasite, açılma kademesi). Yeni ekipman = yeni SO asset, kod değişmez (üst kademe biçme makineleri aynı `MowerTool` davranışını farklı statlarla kullanır).
- Araç tipi aletler (ride-on, Faz 2) `IVehicleTool` genişletmesiyle FP kontrolünden araç kontrolüne geçer (Input Action Map değişimi + Cinemachine kamera geçişi).

### 4.2 Biçme Aleti (Faz 0)

- Stroke modeli: makine ilerledikçe önceki kare pozisyonu → şimdiki pozisyon arasında **kapsül süpürme** hesaplanır; süpürülen CPU hücreleri güncellenir, GPU'ya yön-bilgili stamp basılır.
- Yazılan veri: `GrassHeightMm = kesimYüksekliğiAyarı` (yalnızca mevcut yükseklik > ayar ise), `PatternDir = normalize(hareketYönü)`.
- **Kesim yüksekliği ayarı:** alet üzerinde kademe (scroll/Q-E). Scalping kuralı: ayar < 15 mm ve tek geçişte 20+ mm kesim → hücre sağlığına hasar (GDD §3.1.1).
- Toplanan çim: makine sepeti dolar → boşaltma mikro-görevi (tempo kırıcı, gerçekçilik).
- **Tamamlanma yüzdesi:** CPU grid'den hesaplanır (hedef yükseklik bandındaki hücre oranı) — GPU readback gerekmez, her saniye güncellenir.

### 4.3 Çizgi Aleti (MVP)

- Saha çizgi geometrisi **vektörel tanımdır**: `LineLayoutSO` (FIFA standart ölçüleri parametrik: saha boyutlarına göre üretilen segment + yay listesi).
- Oyuncu çizgi arabasını iterken boya, `RT_Paint`'e stroke olarak basılır; eş zamanlı CPU `PaintCoverage` güncellenir.
- **Düzgünlük skoru:** Oyuncunun stroke noktaları kaydedilir (polyline). Denetimde her stroke, en yakın ideal segmente karşı değerlendirilir: ortalama dikey sapma (cm) + maksimum sapma + kapsama oranı (idealin % kaçı boyandı). Piksel readback gerekmez — skor vektörel hesaptır. (RT yalnızca görseldir.)
- İp gergisi yardımı (Kademe 2 ekipmanı): stroke'u ideal segmente doğru yumuşak mıknatıslama (tam kilitleme değil — beceri korunur).

### 4.4 Sulama Aleti (MVP)

- El hortumu: ray hedefli koni alanına nem ekler (CPU) + nem tint stamp'i (GPU). Basınç ayarı = debi.
- Doz mekaniği CPU tarafında: `Moisture > 85` → su birikintisi işareti (görsel: parlak decal + denetçi kalemi), `Moisture < 30` → kuruma tint'i.
- Sprinkler'lar (Kademe 2+): sahneye yerleştirilen, zamanla alan dolduran pasif aletler — `ITool` değil, `PitchActor` (kendi tick'i olan saha nesnesi).

### 4.5 Onarım Aletleri (MVP)

- Divot çatalı: hedeflenen hasar noktasına tek-tık onarım etkileşimi (animasyonlu, tatmin odaklı). `Damage` ↓, küçük zemin decal temizliği.
- Tohum serpme: hasarlı hücrelere `SeedTimer` başlatır → gün-sonu simülasyonunda filizlenir (P2 gecikmeli ödül).
- Çim rulosu: dikdörtgen alan seç → para karşılığı anında onarım (pahalı hızlı yol).

---

## 5. EVENT CHANNEL + SCRIPTABLEOBJECT TASARIMI

### 5.1 Event Channel Tipleri

Jenerik tabanlı kanal seti (`Events/` altında):

```csharp
// Temel desen
public abstract class EventChannelSO<T> : ScriptableObject {
    private event Action<T> _listeners;
    public void Raise(T payload) => _listeners?.Invoke(payload);
    public void Subscribe(Action<T> l) => _listeners += l;
    public void Unsubscribe(Action<T> l) => _listeners -= l;
}
// Somut: VoidEventChannelSO, IntEventChannelSO, FloatEventChannelSO,
//        DayEndedEventChannelSO(DayReport), GradeEventChannelSO(InspectionReport),
//        TaskProgressEventChannelSO(TaskProgress), EconomyEventChannelSO(EconomyDelta)
```

### 5.2 Ana Kanallar (MVP)

| Kanal Asset'i | Payload | Yayınlayan → Dinleyen |
|---|---|---|
| `OnDayEnded` | `DayReport` (büyüme, kuruma özetleri) | TimeSystem → PitchRenderBridge, UI, SaveSystem (autosave) |
| `OnMatchDayStarted` | `MatchInfo` | TimeSystem → UI (geri sayım), Inspection |
| `OnInspectionCompleted` | `InspectionReport` (kalem notları, derece) | Inspection → Economy, UI (karne ekranı), Reputation |
| `OnMatchFinished` | `MatchDamageReport` | Match → Pitch (hasar stamp), UI |
| `OnToolChanged` | `ToolDefinitionSO` | Player → UI (alet çarkı/HUD) |
| `OnEconomyChanged` | `EconomyDelta` | Economy → UI |
| `OnTaskProgress` | `TaskProgress` (görev id, %) | Tools/Pitch → UI (görev listesi) |

**Kural:** Sistemler arası *tüm* çapraz iletişim kanal üzerinden akar. Doğrudan referans yalnızca aynı asmdef içinde ve aynı alt sistemde serbesttir.

### 5.3 Veri SO Aileleri

| SO | İçerik | Faz |
|---|---|---|
| `ToolDefinitionSO` | Alet statları, model prefab, açılma kademesi, fiyat | 0 |
| `LeagueStandardSO` | Yükseklik bandı + tolerans, zorunlu denetim kalemleri, denetçi sıkılık eğrileri | MVP |
| `StadiumDefinitionSO` | Saha boyutu, çevre prefab, başlangıç grid durumu, kademe | MVP |
| `LineLayoutSO` | Parametrik çizgi geometrisi | MVP |
| `ContractSO` | Taban ücret, karne çarpan eğrisi, özel şartlar (desen talebi vb.) | MVP |
| `InspectionItemSO` | Denetim kalemi tanımı: örneklem stratejisi, puan eğrisi, denetçi yorum havuzu | MVP |
| `MatchDamageProfileSO` | Bölge bazlı hasar yoğunluk haritası, hava çarpanları | MVP |
| `GrassGrowthProfileSO` | Büyüme eğrisi parametreleri (mevsim/sağlık/nem fonksiyonu) | MVP |
| `WeatherPatternSO` | Hava olasılık tabloları, etki çarpanları | Faz 2 |
| `PatternRequestSO` | Desen tanımı + eşleşme değerlendirme parametreleri | Faz 2 |

---

## 6. DENETÇİ / PUANLAMA SİSTEMİ (MVP)

### 6.1 Akış

```
Maç günü deadline → Oyuncu "Denetimi Başlat" → Denetçi NPC saha turu (sahnelenmiş, ~60-90 sn)
→ Kalem kalem ölçüm (aşağıda) → InspectionReport üretilir → Karne ekranı → OnInspectionCompleted
```

### 6.2 Ölçüm Teknikleri (kalem başına)

| Kalem | Teknik | Kaynak |
|---|---|---|
| Çim yüksekliği | CPU grid'den **seed'li rastgele N nokta** örnekleme (N = lig sıkılığı; amatör 5, üst lig 25). Band içi oran → not | CPU grid |
| Çizgi düzgünlüğü | Stroke polyline ↔ ideal geometri vektörel karşılaştırma (bkz. §4.3) | Stroke kaydı |
| Çizgi eksiksizliği | `LineLayoutSO` segment kapsama oranı | Stroke kaydı |
| Nem/oynanabilirlik | Grid: su birikintisi hücre sayısı + kuru bölge oranı | CPU grid |
| Hasar | Grid: `Damage > eşik` hücre oranı | CPU grid |
| Estetik/desen (Faz 2) | `PatternDir` alan tutarlılığı: komşu hücre yön korelasyonu + talep edilen desen şablonuyla eşleşme | CPU grid |

- Not eğrileri ve ağırlıklar `InspectionItemSO`'da — denge ayarı kod değişmeden yapılır.
- Karne: kalem notları (A+–F) + ağırlıklı toplam + denetçi yorum satırları (SO yorum havuzundan, nota göre seçilir; "Sanat eseri mi bu?" tarzı durumsal mizah burada yaşar).
- Denetçi NPC sahne turu **kozmetiktir**; ölçüm matematiksel olarak anında yapılır, tur sonuçları dramatize eder. (Solo kapsam: tur, basit waypoint + bakış animasyonu.)

---

## 7. MAÇ GÜNÜ VE HASAR ÜRETİMİ (MVP)

- Maç **oynanmaz, simüle edilir**: süre atlaması (kısa sahne / skip). Maç izleme sahnesi (GDD R4) Faz 3 adayıdır; MVP'de statik tribün + ses ambiyansı + skip.
- Hasar üretimi: `MatchDamageProfileSO` bölge yoğunluk haritası (kale sahaları 1.0, orta koridor 0.8, kanatlar 0.5, kornerler 0.3) × maç şiddeti × hava çarpanı (Faz 2). Seed'li Poisson-disk dağılımıyla hasar noktaları üretilir → CPU `Damage` + GPU hasar/çamur stamp'leri.
- Hasar tipleri: divot noktaları (tek-tık onarım hedefleri), ezilme alanları (yama/tohum hedefi), çamur lekeleri (yağmurda, Faz 2).

---

## 8. ZAMAN SİSTEMİ (MVP)

- `TimeSystem` (Core): gün fazları (Sabah → Öğle → Akşam), hızlandırma (1×/4×/12×), **"Günü Bitir"** eylemi (yatak/ofis etkileşimi).
- Takvim: `SeasonCalendarSO` — maç günleri sabit işaretli; maç günü `OnMatchDayStarted` ile gerçek-zamanlı geri sayım moduna girer (hızlandırma kilitlenir — P3 baskısı).
- Gün-sonu pipeline sırası (deterministik): `1) Pitch gün-sonu simülasyonu → 2) Ekonomi gün sonu (sarf düşümleri) → 3) Autosave → 4) DayEnded yayını → 5) Sabah özeti UI`.

---

## 9. KAYIT SİSTEMİ (MVP)

### 9.1 Format

```
SaveSlot_01/
├── meta.json        # slot adı, gün, para, itibar, oynama süresi, sürüm
├── state.json       # ekonomi, sözleşme, ekipman envanteri, takvim, görev durumları
└── pitch.bin        # CPU grid blob: header + kanal-bazlı RLE sıkıştırma
```

- **GPU RT'ler kaydedilmez.** Yüklemede CPU grid'den `PitchRenderBridge` ile yeniden üretilir; boya/desen detayı CPU mantıksal değerlerinden tatmin edici şekilde rekonstrükte edilir (boya: `LineLayoutSO` + `PaintCoverage`; desen: `PatternDir` alanı). Bu, kayıt boyutunu ve sürüm-uyumluluk riskini düşürür.
- Stroke polyline kayıtları (çizgi düzgünlüğü için) `state.json`'a sadeleştirilerek (Douglas-Peucker) yazılır.
- Sürümleme: `meta.json.version` + migration fonksiyon zinciri. İlk günden kurulur (sonradan eklemek acıdır).

---

## 10. INPUT VE KAMERA (Faz 0–MVP)

- Action Map'ler: `Player` (WASD, bak, etkileşim, alet kullan, alet çarkı Q, durum tableti Tab), `Vehicle` (Faz 2), `UI`.
- Cinemachine: `FP_Cam` (Faz 0) → `VehicleCam` (Faz 2) → `DroneCam` (Faz 2; desen vitrini + foto modu).
- Durum tableti (Tab): ekran UI'ı, CPU grid katman görselleştirmesi (minimap render: grid → küçük texture, katman filtreleri nem/sağlık/yükseklik/hasar). Diegetik çerçeve ("müdür tableti").

---

## 11. DEBUG VE DOĞRULAMA ALTYAPISI (Faz 0'dan itibaren)

> Solo geliştirme + AI iş ortaklığında doğrulama altyapısı lüks değil zorunluluktur: Claude Code'un ürettiği sistemlerin doğruluğu gözle ve sayıyla denetlenebilmelidir.

- **F1:** Grid katman overlay'i (saha üstüne yarı saydam projeksiyon: nem/sağlık/yükseklik/hasar/desen-yön okları).
- **F2:** Performans HUD (frame ms, çim GPU ms, tick ms, stamp sayısı).
- **F3:** Cheat konsolu: `grow 5mm`, `damage center 50`, `money 1000`, `skipday`, `inspect` — sistemleri izole test etmek için.
- **Birim test edilebilir çekirdek:** Gün-sonu simülasyonu, puanlama fonksiyonları ve çizgi sapma hesabı saf C# (UnityEngine bağımsız) yazılır → EditMode testleri Claude Code tarafından koşulabilir.

---

## 12. UYGULAMA YOL HARİTASI — CLAUDE CODE GÖREV BLOKLARI

> Her blok bağımsız doğrulanabilir bir dikey dilimdir. Bloklar sıralıdır; bir blok "tanım + test + debug görünümü" üçlüsü tamamlanmadan kapanmaz.

### Faz 0 — "Biçmek keyifli mi?" (Bloklar F0.x)

| Blok | İçerik | Doğrulama |
|---|---|---|
| F0.1 | Proje kurulumu: Unity 6 + URP, klasör/asmdef yapısı, Input Actions, Boot→Stadium akışı | Boş sahada FP yürüyüş, 60 FPS |
| F0.2 | `PitchGrid` (CPU) + `CellState` + Burst tick iskeleti + F1 overlay | Overlay'de grid görünür, tick bütçede |
| F0.3 | RT seti + fırça stamp altyapısı (CommandBuffer) + world→UV eşleme | Test fırçasıyla RT'ye iz bırakılıyor |
| F0.4 | Çim render: instanced blade + tile culling + RT örnekleme (yükseklik/renk) | Saha dolu çimle 60 FPS @ bütçe |
| F0.5 | `MowerTool`: kapsül süpürme, çift-katman yazım, kesim yüksekliği ayarı, sepet | Biçme izi anında görünür, % sayacı çalışır |
| F0.6 | Desen: `PatternDir` yazımı + shader şerit yansıması | Düz şerit ve dama deseni gözle seçiliyor |
| F0.7 | His turu: ses (motor + kesim hışırtısı), kamera sallanımı, tamamlanma tatmin anı | **Dış test: "bırakamadım" kriteri** |

### MVP — Döngü kapanışı (Bloklar M.x)

| Blok | İçerik |
|---|---|
| M.1 | TimeSystem + Günü Bitir + gün-sonu simülasyonu (büyüme, solma) + sabah özeti |
| M.2 | `LinerTool` + `LineLayoutSO` + stroke kaydı + düzgünlük hesabı (saf C# + test) |
| M.3 | `HoseTool` + nem simülasyonu + doz uçları (kuruma/birikinti görselleri) |
| M.4 | Inspection: `InspectionItemSO` seti, ölçüm fonksiyonları, karne UI, denetçi turu (basit) |
| M.5 | Match: takvim, maç günü geri sayımı, hasar üretimi, skip sahnesi |
| M.6 | Onarım aletleri (divot/tohum/rulo) + ihmal birikimi (hasar→sağlık erozyonu) |
| M.7 | Economy: para/itibar, `ContractSO`, ekipman satın alma (Kademe 1→2 aletleri) |
| M.8 | SaveSystem (json + pitch.bin + migration iskeleti) + slot UI |
| M.9 | MVP cilası: görev listesi UI, tutorial ipuçları, denge geçişi |

*(Faz 2/3 blokları MVP kapanışında bu formatta detaylandırılacaktır — R3 disiplini gereği şimdi yazılmaz.)*

---

## 13. TEKNİK RİSKLER

| # | Risk | Plan B |
|---|---|---|
| T1 | Instanced blade yaklaşımı bütçeyi aşar | Zemin-texture-ağırlıklı hibrit (blade yalnız ≤10 m); Faz 0.4'te ölçülür, geçilmezse pivot |
| T2 | RT rekonstrüksiyonu (yükleme sonrası) görsel tutarsızlık üretir | Kabul kriteri Faz 0'da tanımlanır; gerekirse `RT_Paint` ham kaydı eklenir (boyut artışı kabul edilir) |
| T3 | Burst/NativeArray öğrenme eğrisi geliştirme hızını düşürür | Tick MVP'de düz C#'la da bütçeye sığar (29.7k hücre küçüktür); Burst optimizasyonu ertelenebilir |
| T4 | Stroke tabanlı çizgi skoru kenar durumları (geri geri çizme, kesişen stroke) | Skor fonksiyonu saf C# + kapsamlı EditMode test seti — kenar durumlar test olarak yazılır |

---

*— TDD v1.0 sonu. Bu doküman, GDD v1.1 ile birlikte Claude Code'a verilecek kaynak settir. Geliştirme F0.1 bloğundan başlar.*
