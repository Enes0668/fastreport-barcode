CREATE TABLE IF NOT EXISTS lab_numune_istekleri (
    id SERIAL PRIMARY KEY,
    istek_id INT NOT NULL,
    barkod_no VARCHAR(50) NOT NULL,
    hasta_adi VARCHAR(100) NOT NULL,
    protokol_no VARCHAR(50) NOT NULL,
    bolum VARCHAR(100) NOT NULL,
    tup_turu VARCHAR(50) NOT NULL
);

INSERT INTO lab_numune_istekleri (istek_id, barkod_no, hasta_adi, protokol_no, bolum, tup_turu)
VALUES 
(504, '869012345001', 'Ahmet Yilmaz', '2026-98451', 'Dahiliye', 'Biyokimya (Kirmizi Tup)'),
(504, '869012345002', 'Ahmet Yilmaz', '2026-98451', 'Dahiliye', 'Hemogram (Mor Tup)'),
(504, '869012345003', 'Ahmet Yilmaz', '2026-98451', 'Dahiliye', 'Sedimantasyon (Siyah Tup)');

SELECT id, istek_id, barkod_no, hasta_adi, tup_turu FROM lab_numune_istekleri WHERE istek_id = 504;
