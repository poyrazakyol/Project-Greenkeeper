# GAME DESIGN DOCUMENT
## PROJECT: GREENKEEPER *(Çalışma Adı)*
### Stadyum Çim Müdürlüğü Simülasyonu

| | |
|---|---|
| **Doküman Sürümü** | v1.2 |
| **Tarih** | 9 Haziran 2026 |
| **Tasarım / Geliştirme** | Poyraz — solo proje |
| **Motor** | Unity (URP) |
| **Platform** | PC (Steam) — birincil hedef |
| **Tür** | First-Person Simülasyon / Management |
| **Oynanış Süresi Hedefi** | 15-25 saat (ana ilerleme), uzatılabilir sezon yapısı |
| **Referans Oyunlar** | PowerWash Simulator (tatmin döngüsü), Supermarket Simulator (işletme ilerlemesi), Lawn Mowing Simulator (alan/ekipman) |

> **Doküman Amacı:** Bu GDD, oyunun tasarım otoritesidir. Teknik uygulama detayları ayrı bir TDD'de tanımlanacaktır. Bu doküman Claude Code'a kaynak materyal olarak verilecektir; bu nedenle her sistem, uygulanabilir netlikte tanımlanmıştır.

---

## 1. OYUN VİZYONU

### 1.1 Tek Cümlelik Tanım (Elevator Pitch)

> Bakımsız bir amatör sahanın çim müdürü olarak başla; çimi biç, deseni işle, çizgileri çek, maç günü denetçiden tam not al — para ve itibar kazanarak ülkenin en büyük stadyumlarının sorumluluğunu üstlen.

### 1.2 Çekirdek Fantezi

Oyuncunun fantezisi "çim biçen kişi" değil, **"sahanın patronu"** olmaktır:

> *"Bugünkü maçın oynanabilirliği, görsel kalitesi ve kulübün itibarı bana bağlı."*

Bu fantezi üç duygu üzerine kuruludur:

1. **Dönüşüm tatmini** — bakımsız, ezilmiş, çamurlu bir sahayı kusursuz hale getirmenin görsel hazzı (PowerWash etkisi).
2. **Sahiplik ve sorumluluk** — saha "level" değil, oyuncunun bakmakla yükümlü olduğu yaşayan bir varlıktır. Maçtan sonra hasarlı döner; ihmal edilirse kalitesi düşer.
3. **Sahne ve aciliyet** — hazırlık bir maç gününe, bir denetime, bir deadline'a bağlıdır. Yapılan iş bir seyirci kitlesi önünde "sahneye çıkar."

### 1.3 Tasarım Sütunları (Design Pillars)

Tüm tasarım kararları bu üç sütuna göre test edilir. Bir özellik bu sütunlardan en az birine hizmet etmiyorsa kapsam dışıdır.

| # | Sütun | Açıklama | Test Sorusu |
|---|---|---|---|
| P1 | **Görsel Tatmin** | Her görevin sonucu gözle görülür olmalı. Görünmez stat değişimi = ölü mekanik. | "Bu işin sonucunu ekranda *görüyor muyum*?" |
| P2 | **Yaşayan Saha** | Saha kalıcıdır; oyuncunun eylemleri ve ihmalleri zamanla birikir. | "Bu sistem sahayla uzun vadeli ilişki kuruyor mu?" |
| P3 | **Patron Baskısı** | Deadline, denetim, itibar — yapılan işin bir bedeli ve seyircisi vardır. | "Bu özellik sorumluluk hissini güçlendiriyor mu?" |

### 1.4 Benzersiz Satış Noktaları (USP)

1. **İki katmanlı görsel ödül:** Hem "kirli→temiz" dönüşümü hem de biçme desenlerinin (şerit, dama, elmas) kendisi estetik ödüldür. Rakip simülatörlerde dönüşüm tek katmanlıdır.
2. **Yaşayan/kirletilen sahne:** PowerWash'ta iş bitince sahne biter; burada maç, oyuncunun eserini ezerek geri getirir. "Hazırla → izle → onar" ritmi türde benzersizdir.
3. **Stadyum fantezisi:** Boş pazar. Lawn Mowing Simulator bahçe/arazi odaklıdır; profesyonel futbol groundskeeping niş ama görünür bir gerçek-dünya hayranlığına sahiptir (groundskeeping/ASMR video kültürü).
4. **Denetçi "boss fight" yapısı:** Her maç günü ölçülebilir, karneli bir performans zirvesiyle biter.

### 1.5 Gerçeklik Referansı: IFAB / FIFA Standartları

Oyundaki tüm saha parametreleri **IFAB Oyun Kuralları** ve **FIFA Saha Standartları**'na paraleldir. Bu hem oyunun otantiklik iddiasını güçlendirir hem denetçi sistemine gerçek bir zemin verir. Aşağıdaki değerler `LeagueStandardSO` ve `LineLayoutSO` veri asset'lerinin referans kaynağıdır.

#### Saha Boyutları

| Parametre | IFAB min–max | FIFA Turnuva (zorunlu) | Oyundaki değer |
|---|---|---|---|
| Uzunluk | 90–120 m | 105 m | 105 m (sabit, turnuva standardı) |
| Genişlik | 45–90 m | 68 m | 68 m (sabit) |
| Çizgi genişliği | — | maks 12 cm | 12 cm |

#### Çim Yüksekliği (kademe karşılıkları)

| Kademe | Standart | Yükseklik bandı | Tolerans |
|---|---|---|---|
| 1 — Amatör | Yerel lig / kulüp | 25–50 mm | ±10 mm |
| 2 — 3. Lig | Ulusal federasyon | 20–35 mm | ±5 mm |
| 3 — 2./1. Lig | FIFA önerilen | 20–30 mm | ±3 mm |
| 4 — Süper Lig | FIFA Elite | 20–25 mm | ±2 mm |
| 5 — Milli/Avrupa | UEFA / FIFA zorunlu | 20–25 mm | ±1 mm |

#### Saha Geometrisi (LineLayoutSO referansı)

| Unsur | Ölçü |
|---|---|
| Ceza sahası (penalty area) | 40.32 m × 16.5 m |
| Kale sahası (goal area) | 18.32 m × 5.5 m |
| Orta yuvarlak yarıçapı | 9.15 m |
| Penaltı noktası mesafesi | 11 m |
| Ceza yayı yarıçapı | 9.15 m (penaltı noktasından) |
| Korner yayı yarıçapı | 1 m |
| Kale genişliği / yüksekliği | 7.32 m / 2.44 m |

> **Tasarım notu:** Bu değerler oynanış açısından sabittir; oyuncu saha boyutunu değiştiremez. Denetçi sistemi çizgi geometrisini bu referans değerlere göre ölçer. Üst liglerde toleransın daralması (`±1 mm` çim yüksekliği, çizgi sapması cm hassasiyeti) aynı mekaniği ilerledikçe zorlaştıran ana mekanizmadır — yeni sistem eklenmez, standart sıkışır.

---

## 2. ÇEKİRDEK OYUN DÖNGÜSÜ

### 2.1 Makro Döngü (Kariyer)

```
İş Teklifi Al → Sözleşme Seç → Sahayı Yönet (sezon) → Para + İtibar Kazan
      ↑                                                        ↓
      └────────── Daha Büyük Stadyum / Ekipman Yatırımı ←──────┘
```

### 2.2 Orta Döngü (Maç Haftası)

Oyunun temel zaman birimi **maç haftasıdır**. Her hafta iki ritimden oluşur:

```
[SEZON RİTMİ — günler]              [MAÇ GÜNÜ RİTMİ — saatler]
Hasar onarımı                       Final biçme + desen
Gübreleme (gecikmeli yatırım)  →    Çizgi çekme
Aerasyon, çim ekimi                 Sulama / son rötuş
Hava durumuna tepki                 DENETÇİ KONTROLÜ (karne)
                                          ↓
                                    MAÇ OYNANIR (saha hasar alır)
                                          ↓
                                    Karne sonucu: Para + İtibar
                                          └──→ döngü başa döner
```

**Tasarım notu:** İki ritim bilinçli olarak ayrıştırılmıştır. Sezon ritmi *planlama ve yatırım* hissi verir (gübre bugün atılır, etkisi 3 gün sonra görülür); maç günü ritmi *baskı ve performans* hissi verir (geri sayım, denetçi, kalabalık). Kod tarafında da bu ayrım `SeasonalCare` ve `MatchDayTask` veri aileleri olarak yansıtılmalıdır.

### 2.3 Mikro Döngü (Dakikalar)

```
Sorunu GÖR (sarı kuru bölge / uzamış çim / silik çizgi)
   → Doğru ALETİ seç
   → UYGULA (fiziksel, elle yapılan iş)
   → ANINDA görsel geri bildirim al
   → Tamamlanma yüzdesi / kalite skoru artar
```

Mikro döngünün kutsal kuralı (P1): **uygulama ile görsel sonuç arasında gecikme olmaz.** Tek istisna bilinçli tasarlanmış gecikmeli sistemlerdir (gübre) ve onların da *uygulama anı* görsel geri bildirim verir (granül serpme efekti, ıslak iz).

### 2.4 Zaman Modeli *(Karara bağlandı)*

- **Hızlandırılabilir gün + manuel "Günü Bitir"** modeli (Stardew Valley benzeri).
- Gün içinde zaman, oyuncu çalışırken doğal hızda akar; oyuncu isterse zamanı hızlandırabilir (örn. sulamanın etkisini beklerken).
- Günün işi bittiğinde oyuncu **"Günü Bitir"** eylemini manuel tetikler → gece geçişi sırasında gecikmeli sistemler işler: çim büyümesi, gübre etkisi ilerlemesi, tohum filizlenmesi, nem buharlaşması, boya solması.
- **Gerekçe:** Gübrenin gecikmeli etkisi, çim büyümesi ve hava takvimi gibi sistemlerin tamamı "gün" birimine ihtiyaç duyar; manuel gün bitirme oyuncuya baskısız tamamlama hissi verir (P2 — bakım meditatiftir), maç günü ise takvimde sabit durduğu için deadline baskısı korunur (P3).
- Maç günü **gerçek zamanlı geri sayım** içerir (saat: maç başlamadan denetim tamamlanmalı). Sezon günlerinde sayaç yoktur.

---

## 3. OYUN SİSTEMLERİ

Sistemler iki kategoriye ayrılır: **Maç Günü Görevleri** (deadline baskısı altında, performans odaklı) ve **Sezonluk Bakım** (planlama odaklı, birikimli etki).

### 3.0 Saha Durum Modeli (Tüm Sistemlerin Temeli)

Saha, hücre bazlı bir durum haritası üzerinde yaşar. Her hücre şu değerleri taşır:

| Parametre | Aralık | Etkileyen Sistemler | Görsel Karşılığı |
|---|---|---|---|
| **Çim Yüksekliği** | 0–60 mm | Biçme (↓), zaman/büyüme (↑) | Çim mesh/shader yüksekliği |
| **Çim Sağlığı** | 0–100 | Gübre (↑), nem dengesi, hastalık (↓), aşırı kullanım (↓) | Renk: canlı yeşil ↔ sarı/kahve; kel noktalar |
| **Nem** | 0–100 | Sulama (↑), yağmur (↑), sıcaklık (↓) | Sararma (kuru) ↔ koyuluk/su birikintisi (aşırı) |
| **Sıkışma (Compaction)** | 0–100 | Maç trafiği (↑), ağır ekipman (↑), aerasyon (↓) | Su birikintisi eğilimi, cılız çim büyümesi |
| **Hasar** | 0–100 | Maç (krampon izi, divot), konser vb. (↑), onarım (↓) | Çukurlar, kopuk çim parçaları, çamur lekeleri |
| **Boya (Çizgi)** | 0–100 | Çizgi arabası (↑), yağmur/aşınma (↓) | Beyaz çizgi opaklığı |
| **Desen Yönü** | vektör | Biçme yönü | Şerit parlaklık farkı (ışık yansıması) |

> **Kritik kural (P1):** Bu parametrelerin HİÇBİRİ yalnızca UI bar'ı olarak var olamaz. Her birinin sahada gözle görülür bir karşılığı vardır. Sıkışma gibi soyut değerler bile dolaylı görünür sonuç üretir (su birikintisi, cılız çim).

---

### 3.1 MAÇ GÜNÜ GÖREVLERİ

#### 3.1.1 Çim Biçme

| | |
|---|---|
| **Oyuncu eylemi** | Biçme makinesini sahada sürmek/itmek; biçme yüksekliğini ayarlamak |
| **Oyun karşılığı** | Çim yüksekliğini lig standardı aralığa getirmek |
| **Sütun** | P1 (anında görsel sonuç), P3 (standart uygunluğu) |

- Her lig seviyesinin bir **yükseklik standardı** vardır (gerçekçilik referansı: profesyonel futbolda ~20–30 mm). Amatör ligde tolerans geniştir (15–45 mm kabul), üst liglerde daralır (UEFA seviyesinde ±2 mm).
- Biçme makinesinin **yükseklik ayarı** oyuncu kontrolündedir. Yanlış ayarla biçmek "iş yapılmış ama standart dışı" sonucu üretir — denetçi yakalar.
- Çok kısa biçmek (scalping) çim sağlığına hasar verir → P2 ile bağlantı: maç günü acele kararları sezonluk bedel öder.
- Çim **zamanla uzar** (büyüme hızı: mevsim, nem, gübre durumuna bağlı). Biçme tek seferlik değil, periyodik iştir.

#### 3.1.2 Desen / Şerit Oluşturma

| | |
|---|---|
| **Oyuncu eylemi** | Biçme yönünü bilinçli planlayarak desen işlemek |
| **Oyun karşılığı** | Estetik puan; kulüp/denetçi desen talepleri |
| **Sütun** | P1 (oyunun imza görseli), P3 (talep edilen deseni tutturma) |

- Desen, biçme makinesinin **geçiş yönünün** kaydedilmesiyle oluşur; çim yatış yönü ışığı farklı yansıtır (açık/koyu şerit).
- Desen tipleri (ekipman ve ilerlemeyle açılır): Düz şerit → Dama (checkerboard) → Elmas → Daire/orta yuvarlak vurgusu.
- Kulüpler sözleşmede **belirli desen talep edebilir** ("Derbi için elmas desen istiyoruz"). Talep edilen desene uygunluk estetik puanın parçasıdır.
- Estetik puan ölçümü: şerit düzgünlüğü (paralellik), şerit genişlik tutarlılığı, talep edilen desenle eşleşme.
- **Tasarım notu:** Desen sistemi oyunun ekran görüntüsünü "satan" özelliktir; pazarlama materyallerinin merkezindedir.

#### 3.1.3 Çizgi Çekme

| | |
|---|---|
| **Oyuncu eylemi** | Çizgi arabasını iterek/sürerek saha çizgilerini boyamak |
| **Oyun karşılığı** | Kural uygunluğu (FIFA/lig ölçüleri) + düzgünlük skoru |
| **Sütun** | P1, P3 (denetçinin en sıkı baktığı kalem) |

- Çizilecek elemanlar: taç çizgileri, kale çizgileri, orta çizgi, orta yuvarlak, ceza sahaları, kale sahaları, penaltı noktaları, ceza yayları, korner yayları.
- **Düzgünlük skoru:** Oyuncunun çektiği çizgi, ideal geometriyle karşılaştırılır; sapma puanı düşürür. Titrek çizgi hem komik anlar üretir hem denetçi karnesine yansır.
- Yardımcı sistemler ilerlemeyle açılır: ip gergisi (düşük seviye yardım) → işaretleme şablonları → yarı otomatik çizgi robotu (geç oyun).
- Boya **zamanla ve yağmurla silikleşir**; her maç öncesi tazelenmesi gerekir.
- Yanlış geometri (eksik çizgi, yamuk ceza sahası) denetçi kontrolünde **kırmızı kalem** = maç onayı riski.

#### 3.1.4 Sulama

| | |
|---|---|
| **Oyuncu eylemi** | Hortum/sprinkler ile bölgesel nem dengesi kurmak |
| **Oyun karşılığı** | Nem dengesini hedef bantta tutmak (az = kuruma, çok = çamur) |
| **Sütun** | P1 (anında renk değişimi), P2 (çim sağlığına girdi) |

- **Doz mekaniği (kritik):** Sulama "kapla ve geç" değil, "dengele" görevidir. Az su → sararma ve sağlık kaybı; aşırı su → su birikintisi, çamurlaşma, maç günü oynanabilirlik riski.
- Görsel geri bildirim anlıktır: kuru bölge gözle görülür sarıdır, sulanınca koyulaşır; aşırı sulanan bölgede parlak su birikintisi oluşur.
- Hava durumu çarpanı: sıcak dalga nem tüketimini 2 katına çıkarır; yağmur günü sulama gereksizdir (hatta zararlıdır).
- Ekipman ilerlemesi: el hortumu → taşınabilir sprinkler → gömülü otomatik sprinkler sistemi (bölge bazlı zamanlama — sezon ritmine otomasyon getirir).

#### 3.1.5 Maç Günü Kontrolü — Denetçi Sistemi ("Boss Fight")

| | |
|---|---|
| **Oyuncu eylemi** | Tüm hazırlığı deadline'a kadar bitirip denetçiyi karşılamak |
| **Oyun karşılığı** | Karne → para çarpanı + itibar + maç onayı |
| **Sütun** | P3 (oyunun ritmik zirvesi) |

- Denetçi sahayı **kalem kalem gezer**: çim yüksekliği ölçümü (rastgele noktalardan), çizgi geometri/düzgünlük kontrolü, nem/oynanabilirlik kontrolü, görsel/desen değerlendirmesi.
- Sonuç: **Karne ekranı** — her kalem için ayrı not + toplam derece (örn. A+ … F). Karne hem ödül çarpanıdır hem öğretmendir: oyuncu neyi ihmal ettiğini görür.
- **Lig seviyesi = denetçi sıkılığı.** Amatör ligde denetçi çim yüksekliği ölçmez, çizgilerin "var olması" yeter. Üst liglere çıkıldıkça tolerans daralır ve yeni kalemler eklenir (nem dengesi, desen talebi, hibrit çim durumu). Böylece aynı sistemler yeni mekanik eklenmeden derinleşir.
- Kritik başarısızlık: saha "oynanamaz" raporu alırsa maç ertelenir → ağır itibar kaybı + ceza. (Nadir, ancak tehdidin gerçek olması P3 için şarttır.)

---

### 3.2 SEZONLUK BAKIM SİSTEMLERİ

#### 3.2.1 Hasar Onarımı ⭐ *(Döngüyü var eden sistem)*

| | |
|---|---|
| **Oyuncu eylemi** | Maç sonrası krampon izlerini, divot'ları, ezilmiş/çamurlaşmış bölgeleri onarmak |
| **Oyun karşılığı** | Uzun vadeli saha kalitesi; ihmal birikir |
| **Sütun** | P2'nin kalbi |

- Maç, sahaya **gerçekçi dağılımlı hasar** bırakır: kaleci sahaları ve orta saha en yoğun, kanatlar orta, korner bölgeleri hafif. Yağmurlu maç hasarı belirgin artırır (çamur deryası → ekstra iş, ekstra ücret).
- Onarım türleri: divot doldurma (tek tek, elle — tatmin edici mikro-etkileşim), kum/toprak serme, **çim ekimi** (kel noktalara tohum → günler içinde büyür, P2 gecikmeli ödül), çim rulosu yama (hızlı ama pahalı).
- **İhmal birikir:** Onarılmayan hasar çim sağlığını düşürür, bir sonraki maçta hasar daha hızlı büyür. Saha oyuncuyla *ilişki* kurar; level değildir.
- **Duygusal tasarım:** Oyuncu maçı tribünden/saha kenarından izleyebilir (kısaltılmış sahne). Kendi işlediği desenin üstünde gol sevinci yaşanır; ardından "şimdi bunu ben temizleyeceğim" gerilimi. Bu sahne hem ödül hem döngünün duygusal yakıtıdır.

#### 3.2.2 Gübreleme

| | |
|---|---|
| **Oyuncu eylemi** | Zayıf bölgelere gübre uygulamak |
| **Oyun karşılığı** | Gecikmeli sağlık + büyüme hızı yatırımı |
| **Sütun** | P2 (takvim planlaması) |

- **Gecikmeli etki (kritik tasarım kararı):** Gübre bugün atılır, sonuç ~3 gün sonra görülür. Maç sabahı gübre atmak işe yaramaz. Bu, gübreyi maç günü görevinden çıkarıp **sezon planlama** katmanına taşır ve "patron takvim düşünür" fantezisini besler.
- Uygulama anının kendi görsel geri bildirimi vardır (granül serpme, uygulama izi) — P1 ihlal edilmez.
- Aşırı gübre riski: yakma (sararma). Doz yine önemlidir.
- Gübre türleri ilerlemeyle açılır: temel → mevsimlik formüller → sıvı hızlı etkili (geç oyun, "acil durum" aracı).

#### 3.2.3 Havalandırma (Aerasyon)

| | |
|---|---|
| **Oyuncu eylemi** | Aerasyon makinesiyle sıkışmış bölgeleri delmek |
| **Oyun karşılığı** | Sıkışma azaltma → su birikintisi ve cılız büyüme sorununu çözme |
| **Sütun** | P1'e bağlanmış P2 |

- **Tasarım kuralı:** Aerasyon görünmez bir bar düşürmez; görünür bir sorunu çözer. Sıkışmış bölgeler kendini **su birikintisi eğilimi** ve **cılız/seyrek çim** olarak gösterir. Aerasyon bu görsel sorunların kök çözümüdür.
- Uygulama izi görünürdür (delik deseni, birkaç gün hafif görünür kalır — gerçekçi detay).
- **Kapsam kararı: MVP'de YOK.** Aerasyon, orta-oyun ekipman kilidi olarak girer (bkz. §5). Sıkışma parametresi MVP'de sabit/pasif tutulur.

#### 3.2.4 Hava Durumu *(Sistem değil — çarpan)*

Hava durumu kendi başına bir görev üretmez; mevcut tüm sistemleri büker:

| Hava | Etkisi |
|---|---|
| **Yağmur** | Sulama iptal/zararlı; çizgi boyası hızlı silinir; maç hasarı ↑ (çamur); aşırı nem riski |
| **Sıcak dalga** | Nem tüketimi ×2; kuruma/sararma hızlanır; sulama kritikleşir |
| **Don / kar** | Çim büyümesi durur; saha ısıtma sistemi (geç oyun ekipmanı) anlam kazanır; kar temizleme özel işi |
| **Rüzgâr** | Çizgi boyama hassasiyeti düşer (hafif zorluk çarpanı) |

- Hava **tahmini** oyuncuya önceden verilir (haftalık tahmin paneli) → planlama kararı üretir: "Cuma yağmur var, çizgileri cumartesi sabahı çekeyim."
- Tahmin %100 isabetli değildir (üst zorlukta sürpriz değişimler).

#### 3.2.5 Saha Kalitesinin Maça Etkisi *(Fanteziyi kapatan geri bildirim)*

"Maçın oynanabilirliği bana bağlı" cümlesinin oyun içi karşılığı:

- **Kötü saha:** Maç özetinde top sekme/kontrol şikâyetleri, oyuncu sakatlanma riski haberi, kulüp basınının saha eleştirisi → itibar ↓.
- **Kusursuz saha:** Teknik direktör demeci ("Bu çimde oynamak zevkti"), taraftar/sosyal medya övgüsü, "Haftanın Sahası" nominasyonu → itibar ↑.
- Uygulama biçimi: maç sonrası **haber/yorum kartları** (metin + basit görsel). Tam maç simülasyonu gerekmez; algı geri bildirimi yeterlidir.
- **Kapsam:** İkinci halka (MVP sonrası), ancak fantezinin tamamlanması için yüksek öncelikli.

#### 3.2.6 Özel İşler (Çeşitlilik Enjeksiyonu)

Sezon takvimine serpiştirilen, normal ritmi kıran tek seferlik sözleşmeler:

- **Konser sonrası onarım:** Sahne ağırlığının öldürdüğü dev çim bölgeleri; yoğun rulo yama işi, yüksek ücret.
- **Kar temizliği:** Kış maçı öncesi acil temizlik (deadline baskısı maksimum).
- **Saha dönüşümü:** Ragbi/Amerikan futbolu çizgileri için geçici dönüşüm ve geri çevirme (geç oyun).
- **Acil kurtarma:** Başka stadyumun rezil sahasını maça yetiştirme ("danışman müdür" fantezisi, itibar vitrini).

---

## 4. İLERLEME VE EKONOMİ

### 4.1 İki Kaynak Modeli

| Kaynak | Kazanım | Harcama |
|---|---|---|
| **Para (₺)** | Maç günü karnesi (taban ücret × karne çarpanı), özel işler, sezon sözleşme primi | Ekipman, sarf malzemesi (gübre, boya, tohum, çim rulosu), sistem kurulumları (sprinkler, ısıtma) |
| **İtibar (⭐)** | Karne dereceleri, maç etkisi haberleri, özel iş başarıları, sezon bütünlüğü bonusu | Harcanmaz — **eşik kaynağıdır**: yeni lig/stadyum sözleşmeleri itibar eşiğiyle açılır |

- **Tasarım gerekçesi:** Para "ne kadar hızlı büyürüm", itibar "nereye erişebilirim" sorusunu yönetir. İkisi ayrışınca oyuncu hem kısa vadeli (karne maksimizasyonu) hem uzun vadeli (itibar inşası) hedef taşır.
- İtibar **düşebilir**: kötü karne, ertelenen maç, ihmal edilen saha. Kayıp riski P3'ü canlı tutar.

### 4.2 Kariyer Kademeleri

| Kademe | Mekân | Açılan Ekipman/Sistem | Denetçi Sıkılığı |
|---|---|---|---|
| 1 | **Köy halı sahası / amatör kulüp** (ot bürümüş, çizgisiz) | İtme biçme makinesi, el hortumu, elle çizgi arabası | Yok denecek kadar az — öğretici doğal zorluk |
| 2 | **3. Lig stadı** (~5.000 kişilik) | Motorlu biçme, taşınabilir sprinkler, ip gergisi | Temel kontroller başlar |
| 3 | **2. Lig / 1. Lig stadı** | **Binilen biçme makinesi (ride-on)** ⭐, yarı otomatik çizgi robotu, gömülü sprinkler | Yükseklik ölçümü + geometri kontrolü |
| 4 | **Süper Lig stadyumu** | Hibrit çim sistemi, saha ısıtması, büyüme lambaları | Sıkı tolerans + desen talepleri |
| 5 | **Milli stadyum / Avrupa finali** | Tüm sistemler + final senaryosu | Maksimum — UEFA milimetre bakar |

- **Ride-on biçme makinesi oyunun "wow" anıdır.** İlk biniş anı özel olarak sahnelenir (kısa tanıtım, motor sesi, hız hissi). Ekipman ilerlemesinin felsefesi: *daha hızlı değil, daha keyifli.*
- Kademeler arası geçiş zorunlu değildir; oyuncu alt kademede itibar/para biriktirmeye devam edebilir (sandbox esnekliği).

### 4.3 Ekipman Ağacı (Özet)

| Hat | Seviye 1 → | Seviye 2 → | Seviye 3 |
|---|---|---|---|
| **Biçme** | İtme (manuel) | Motorlu itme | Ride-on / silindirli (desen kalitesi ↑) |
| **Sulama** | El hortumu | Taşınabilir sprinkler | Gömülü otomatik sistem (zamanlanabilir) |
| **Çizgi** | Elle araba + göz kararı | İp gergisi + şablon | Yarı otomatik çizgi robotu |
| **Bakım** | El aletleri (divot çatalı) | Aerasyon makinesi, mibzer | Hibrit çim, ısıtma, büyüme lambası |

- Üst seviye ekipman alt görevleri **otomatikleştirmez, hızlandırır ve zenginleştirir** (örn. silindirli makine daha keskin şerit kontrastı üretir). İstisna: gömülü sprinkler gibi geç oyun otomasyonları, oyuncunun dikkatini yeni açılan derinliklere (desen talepleri, hibrit çim bakımı) kaydırmak için bilinçlidir.

### 4.4 Sözleşme Yapısı

- Her kademede kulüpler **sezonluk sözleşme** teklif eder: maç başı taban ücret + karne çarpanı + sezon sonu bütünlük bonusu (sezon ortalama karnesi).
- Sözleşmelerde **özel şartlar** olabilir: desen talebi, minimum karne taahhüdü (altına düşersen ceza), derbi haftası bonusları.
- Oyuncu aynı anda **tek sahadan** sorumludur (odak ve P2 sahiplik hissi için; çoklu saha yönetimi olası sequel/geç oyun genişlemesidir, kapsam dışı).

---

## 5. GELİŞTİRME FAZLARI VE KAPSAM

### 5.1 Faz 0 — Çekirdek His Prototipi *(her şeyden önce)*

> **Tek soru: Biçmek keyifli mi?** Keyifli değilse hiçbir ekonomi sistemi onu kurtarmaz.

İçerik: düz saha, first-person kontrol, itme makinesi, çim durum haritası (yükseklik + desen yönü), tamamlanma yüzdesi. Menü yok, ekonomi yok, stadyum yok.

**Çıkış kriteri:** Dış test eden en az bir kişiden (arkadaş, oyuncu tanıdık) "bırakamadım, sahayı bitirdim" tepkisi. Solo geliştirmede kendi işine körleşmek kolaydır; bu fazda dış göz zorunludur.

### 5.2 Faz 1 — MVP (Dikey Dilim)

| Dahil | Hariç |
|---|---|
| Biçme + yükseklik standardı | Aerasyon (sıkışma pasif) |
| Desen (düz şerit + dama) | Gübre |
| Çizgi çekme + düzgünlük skoru | Maç etkisi haber sistemi |
| Sulama (doz mekaniği) | Özel işler |
| Denetçi karnesi (3-4 kalem) | Hava durumu (sabit güneşli) |
| Maç hasarı + temel onarım (divot/yama) | Kademe 3+ stadyumlar |
| Tek küçük stadyum (Kademe 1-2) | |
| Temel para/itibar döngüsü | |

**Gerekçe:** Çekirdek döngü (hazırla → denetim → maç → onar) MVP'de tam kapanmalıdır. Hasar onarımı "ekstra" değil, döngünün kapanış halkasıdır — MVP'dedir.

### 5.3 Faz 2 — İkinci Halka

Hava durumu çarpanı, gübre (gecikmeli yatırım), maç etkisi geri bildirimi, Kademe 3 + ride-on makinesi, ek desenler.

### 5.4 Faz 3 — Üçüncü Halka

Aerasyon + sıkışma aktifleşir, özel işler, Kademe 4-5, hibrit çim/ısıtma/lamba sistemleri, sezon sözleşme derinliği, final senaryosu.

---

## 6. OYUNCU DENEYİMİ

### 6.1 Kamera ve Kontroller

- **First-person** birincil kamera (PowerWash paraleli; iş hissinin merkezi).
- Ride-on ekipmanlarda araç kamerası (FP + opsiyonel 3. şahıs).
- **Drone/kuş bakışı modu:** Desen ve genel saha durumunu değerlendirmek için (ilerlemeyle açılır; estetik ödülün vitrinidir — fotoğraf modu entegrasyonu).
- Kontrol şeması: WASD + fare; alet çarkı (Q); durum haritası görünümü (Tab — nem/sağlık/yükseklik katman filtreleri).

### 6.2 UI İlkeleri

- **Diegetik tercih edilir:** Çim yüksekliği elle ölçüm aletiyle, nem göz/renkle okunur. Tab durum haritası "müdür tableti" olarak çerçevelenir.
- Maç günü geri sayımı görünür ve baskı kurar (P3); sezon günlerinde zaman baskısı yoktur (P2 — bakım meditatiftir).
- Karne ekranı oyunun imza UI'ıdır: kalem kalem notlar, denetçi yorumu, para/itibar dökümü.

### 6.3 Ton ve Atmosfer

- **Meditatif iş + maç günü gerilimi** kontrastı. Sezon günleri sakin (ortam sesi, kuş, uzak şehir); maç günü kalabalık uğultusu yaklaştıkça yükselir.
- Mizah hafif dozda ve durumsaldır (titrek çizgi karneye "Sanat eseri mi bu?" denetçi yorumu düşürür) — oyun kendini ciddiye alan bir simülasyon, parodi değildir.
- Ses tasarımı yatırım noktasıdır: biçme makinesi motor karakteri, çim kesim hışırtısı, sprinkler ritmi — ASMR değeri P1'in işitsel uzantısıdır.

---

## 7. RİSKLER VE AÇIK SORULAR

| # | Risk / Soru | Etki | Mevcut Duruş |
|---|---|---|---|
| R1 | **Çim render + durum sistemi performansı** (oyunun teknik bel kemiği) | Kritik | Faz 0'ın varlık sebebi; TDD'nin ana konusu |
| R2 | Sulama/gübre/aerasyonun "alet al, alanı tara" tekrarlılığı | Yüksek | Çözüldü: doz mekaniği / gecikmeli etki / görünür sorun çözümü olarak ayrıştırıldı (§3) |
| R3 | **Solo geliştirme → kapsam taşması** (en olası başarısızlık nedeni) | Yüksek | Faz disiplini + "tek saha" kuralı + sütun testi. Faz 0 ve MVP dışındaki hiçbir içerik, MVP kapanmadan üretilmez. Claude Code iş ortağıdır ancak kapsam kararları bu dokümana bağlıdır |
| R4 | Maç izleme sahnesinin maliyeti (oyuncu animasyonları) | Orta | Kısaltılmış/stilize sahne; tam maç simülasyonu yok. Gerekirse Faz 3'e ertelenir. Solo kapsamda stilize/silüet çözüm önceliklidir |
| S1 | ~~Gerçek zaman akışı mı, gün-atlama mı?~~ | — | ✅ **KARAR:** Hızlandırılabilir gün + manuel "Günü Bitir" (bkz. §2.4) |
| S2 | Oyun adı | Pazarlama | "Project: Greenkeeper" çalışma adı; isim araştırması Faz 1 sonunda |

---

## 8. SÖZLÜK

| Terim | Anlam |
|---|---|
| **Divot** | Krampon/darbe ile kopan çim parçası ve bıraktığı çukur |
| **Scalping** | Çimi aşırı kısa biçerek kök sistemine zarar verme |
| **Aerasyon** | Sıkışmış toprağı delerek hava/su geçirgenliği kazandırma |
| **Hibrit çim** | Doğal çim + sentetik fiber takviyesi (üst düzey stadyum standardı) |
| **Splatmap / Durum haritası** | Saha hücre durumlarını tutan veri katmanı (teknik detay TDD'de) |
| **Karne** | Denetçi değerlendirme sonucu; para çarpanı ve itibar kaynağı |

---

*— GDD v1.1 sonu. Sonraki doküman: TDD (Teknik Tasarım Dokümanı) — çim render mimarisi, durum haritası veri modeli, ScriptableObject + Event Channel uygulaması, zaman/gün sistemi, kayıt sistemi.*
