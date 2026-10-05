-- ==============================================================================
-- FASTREPORT POSTGRESQL ENTEGRASYON ŞEMASI & TEST VERİSİ
-- ==============================================================================
-- Baş mühendisin bahsettiği "FastReport içinden PostgreSQL'e sorgu atma" mimarisi.
-- Bu tablolar HBYS veya Laboratuvar veritabanında yer alır.

-- 1. TABLO TANIMI
CREATE TABLE IF NOT EXISTS lab_numune_istekleri (
    id SERIAL PRIMARY KEY,
    istek_id INT NOT NULL,                  -- Örneğin bir hekimin tek seferde verdiği tahlil emri
    barkod_no VARCHAR(50) NOT NULL,        -- Tüpün üzerine basılacak tekil barkod
    hasta_adi VARCHAR(100) NOT NULL,
    protokol_no VARCHAR(50) NOT NULL,
    bolum VARCHAR(100) NOT NULL,
    tup_turu VARCHAR(50) NOT NULL          -- Biyokimya, Hemogram, Hormon, İdrar vb.
);

-- 2. ÖRNEK TEST VERİSİ (1 İstek, N Adet Numune)
-- Tek bir istek_id = 101 için 3 farklı kan tüpü emri:
INSERT INTO lab_numune_istekleri (istek_id, barkod_no, hasta_adi, protokol_no, bolum, tup_turu)
VALUES 
(101, '869012345001', 'Ahmet Yılmaz', '2026-98451', 'Dahiliye', 'Biyokimya (Kırmızı Kapak)'),
(101, '869012345002', 'Ahmet Yılmaz', '2026-98451', 'Dahiliye', 'Hemogram (Mor Kapak)'),
(101, '869012345003', 'Ahmet Yılmaz', '2026-98451', 'Dahiliye', 'Sedimantasyon (Siyah Kapak)');

-- ==============================================================================
-- 3. FASTREPORT TASARIMCISINDA YAPILACAK ADIMLAR:
-- ==============================================================================
-- 1. Uygulamada "Sürükle-Bırak Etiket Tasarımcısını Aç" butonuna tıklayın.
-- 2. Sağ taraftaki "Data" (Veri) panelinde "Add Data Source" (Yeni Veri Kaynağı) deyin.
-- 3. Açılan pencerede "PostgreSQL Connection" seçeneğini seçin.
-- 4. Bağlantı bilgilerini girin:
--    Host: localhost
--    Database: hbys_barkod
--    User: postgres
--    Password: ***
-- 5. "Add SQL Query" (SQL Sorgusu Ekle) seçin ve şu sorguyu yazın:
--
--    SELECT barkod_no, hasta_adi, protokol_no, bolum, tup_turu 
--    FROM lab_numune_istekleri 
--    WHERE istek_id = @IstekId
--
-- 6. Sorgu parametresi olarak:
--    Name: IstekId
--    DataType: Int
--    Expression: [IstekId]
-- 7. DataBand üzerine:
--    - Barcode1.Expression = [Table.barkod_no]
--    - txtHastaAdi.Text = [Table.hasta_adi]
--    - txtProtokolNo.Text = [Table.protokol_no]
--    - txtBolum.Text = [Table.bolum] + ' - ' + [Table.tup_turu]
-- 8. Tasarımı kaydedip çıkın.
-- ==============================================================================
