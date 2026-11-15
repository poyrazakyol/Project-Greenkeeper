# CLAUDE.md — Project: Greenkeeper

Stadyum çim müdürlüğü simülasyonu. Unity 6 LTS + URP, C#, solo geliştirici (Poyraz). First-person simülasyon: çim biçme, desen, çizgi çekme, sulama, denetçi karnesi, maç hasarı döngüsü.

## Otorite Dokümanları

Bu repo iki otorite dokümanına bağlıdır. Her oturumda göreve başlamadan ilgili bölümleri oku:

- `Docs/GDD_Project_Greenkeeper_v1.2.md` — **tasarım otoritesi** (ne yapıyoruz, neden)
- `Docs/TDD_Project_Greenkeeper_v1.1.md` — **teknik otorite** (nasıl yapıyoruz)

Çelişki durumunda: tasarım sorusu → GDD, teknik soru → TDD. Bu dokümanları **asla kendiliğinden değiştirme**; değişiklik gerekiyorsa öner ve onay bekle.

## Mevcut Durum

- **Aktif faz:** MVP — "Döngü kapanışı" (Faz 0 ✅ 2026-06-12: dış test "bırakamadım" kriteri geçti)
- **Aktif blok:** M.4 (Inspection — `InspectionItemSO` seti, ölçüm fonksiyonları, karne UI, denetçi turu; M.3.5 ✅ 2026-11-15)
- Blok tanımları: TDD §12. Blok ilerleyişi `Docs/PROGRESS.md` dosyasında takip edilir; bir blok kapanınca orayı güncelle.

## Kapsam Disiplini (EN ÖNEMLİ KURAL)

- Yalnızca aktif bloğun kapsamında çalış. Sıradaki bloğa geçiş **yalnızca benim onayımla** olur.
- Faz 2/3 sistemlerini (hava durumu, gübre, aerasyon, ride-on, maç etkisi haberleri) **uygulama, iskelet kodunu bile yazma.** Mimaride yer açmak serbest (interface/SO tanımı), implementasyon yasak.
- Kapsam dışı özellik önerme. İyileştirme fikrin varsa tek satırla not et (`Docs/IDEAS.md`), uygulamaya geçme.
- Bir blok şu üçlü tamamlanmadan kapanmaz: **çalışan tanım + test (mümkünse) + debug görünümü.**

## Mimari Kurallar (TDD'nin özü — ihlal etme)

1. **Asmdef bağımlılık oku tek yönlü:** Core ← Pitch ← Tools ← Gameplay ← UI. Ters yönlü referans ekleme.
2. **Sistemler arası iletişim yalnızca Event Channel SO'ları üzerinden.** Çapraz sistem doğrudan referans, singleton erişimi veya `FindObjectOfType` yok.
3. **CPU/GPU otorite ayrımı:** Oyun mantığı (puanlama, simülasyon, kayıt) yalnızca `PitchGrid` (CPU) okur. GPU RenderTexture'ları yalnızca görseldir; mantık için GPU readback yapma (tek istisna TDD §3.1'de tanımlı).
4. **Veri SO'dadır:** Statlar, eğriler, standartlar hard-code edilmez; ilgili `*SO` asset'inden okunur.
5. **Gün-sonu simülasyonu ve puanlama fonksiyonları saf C#** (UnityEngine bağımsız) kalır — EditMode test edilebilirlik bozulamaz.

## Kod Standartları

- C# konvansiyonları: PascalCase tipler/metotlar, _camelCase private alanlar, `SO` soneki ScriptableObject tiplerinde.
- Dosya yerleşimi TDD §2.1 klasör yapısına uyar; yeni klasör açmadan önce sor.
- Yorumlar ve commit mesajları **İngilizce**; benimle iletişim **Türkçe**.
- Yeni Unity paketi / 3rd party bağımlılık eklemek **onay gerektirir**.
- Her mantık değişikliğinde ilgili EditMode testlerini çalıştır; kırmızı test bırakma.

## Doğrulama (Unity CLI)

Doğrulama artık açık Editor üzerinden Unity CLI + `com.unity.pipeline` ile yapılır. Batchmode yalnız Editor kapalıyken (veya CI'da) yedektir.

1. **Bağlantı:** `unity status --until-ready --project-path C:\SimulatorProject` → state `ready` olmalı. Bağlanamazsa önce Safe Mode'u ele (`unity pipeline list`); sessizce batchmode'a ya da elle dosya düzenlemeye geçme, durumu bildir.
2. **Derleme:** `unity recompile --project-path C:\SimulatorProject` → 0 hata.
3. **EditMode testleri:** `unity command "test run" --detach --project-path C:\SimulatorProject -- --mode editor`, ardından `unity job wait <jobId> --timeout 600` → `Failed: 0`. (`unity test` Editor açıkken proje kilidine takılır; yalnız Editor kapalıyken kullanılır.)
4. **Play kontrolü** (sahne/oynanış değişikliklerinde): `scene open` Boot → `editor.playmode start` → `unity status --until-ready` → `job wait` ile `UnityEngine.Time.frameCount` değişimi (donma kontrolü) → `console get -- --level warn --since <cursor>` → `editor.playmode stop`. Console'u temizleme; imleçle yalnız yeni girdileri oku.
5. Komut adları paket sürümüyle değişir: kullanmadan önce `unity command --query <terim> --detail full` ile doğrula. Editor parametreleri `--` ayracından sonra yazılır.
6. Kullanıcının açık sahnesinde kaydedilmemiş değişiklik varsa sahne açmadan önce sor (`scene list_open` → `isDirty`).
7. **Paket/manifest değişikliğinden sonra** açık Editor değişikliği ancak penceresi odaklanınca yükler (`unity status` → `STATUS_PIPELINE_LOAD_PENDING`). Bu durumda kullanıcıdan Unity penceresine tıklamasını iste, ardından `unity status --until-ready` ile bekle.
8. **CLI binary'sini onaysız güncelleme** (`unity self-update`). Güncelleme bildirimi görürsen kullanıcıya ilet, kararı ona bırak.

## Çalışma Akışı

1. Görev aldığında önce ilgili TDD bölümünü ve aktif blok tanımını özetle, planını kısaca sun, sonra uygula.
2. TDD'den sapma gerektiren bir durum görürsen **durup sor** — sessizce farklı bir çözüm uygulama.
3. Büyük değişikliklerden önce küçük, derlenebilir adımlar halinde ilerle; her adımda derleme bütünlüğünü koru.
4. Performans bütçeleri (TDD §1.3) tasarım kriteridir; bütçeyi aşan çözümü "çalışıyor" diye kabul etme, bildir.

## Asla Yapma

- GDD/TDD'yi onaysız değiştirme.
- Faz disiplinini "madem buradayız" diyerek delme.
- `Library/`, `Temp/`, `*.csproj` gibi Unity üretimi dosyaları commit'leme (.gitignore'a uy).
- Test edilmemiş kayıt/yükleme değişikliği bırakma (save bozulması solo projede telafisi en pahalı hatadır).
