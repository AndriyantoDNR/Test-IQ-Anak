# UAT Browser Working Memory

## Menjalankan aplikasi

1. Jalankan PostgreSQL dengan `start-postgres.ps1` di `C:\Users\62210770\devtools\postgresql-scripts`.
2. Jalankan API dari Visual Studio memakai profile `http`. API tersedia di `http://localhost:5138`.
3. Dari `src/siap-sd-cognitive-web`, jalankan `npm run dev`. Frontend tersedia di `http://127.0.0.1:5173`.

## Browser E2E

Jalankan suite Playwright setelah dependensi dan browser project tersedia. Test perlu membuat child `WM Browser UAT`, memulai assessment melalui UI, lalu menjawab item standar hingga mencapai setiap subtest Working Memory. Respons tidak boleh dimasukkan langsung ke PostgreSQL.

## Pemeriksaan otomatis

- API dan frontend dapat diakses.
- Jawaban scalar soal standar diterima.
- Jawaban scalar untuk soal memori dan jawaban terstruktur untuk soal standar mendapat HTTP 400.
- Respons memori menyimpan snapshot, telemetry, dan waktu respons.
- Telemetry berisi panjang urutan, posisi benar, akurasi, status penuh benar, tipe kesalahan, dan modality.
- Speech API menggunakan `id-ID`; input terkunci selama playback dan dibersihkan saat komponen dilepas.

## Pemeriksaan manual

- [ ] Animasi visual mudah dipahami.
- [ ] Highlight spatial mudah dibedakan.
- [ ] Suara Bahasa Indonesia terdengar jelas.
- [ ] Tidak ada suara bertumpuk.
- [ ] Tombol cukup besar untuk anak.
- [ ] Instruksi mudah dipahami.

Kualitas volume, pelafalan, naturalness, dan kejelasan suara memerlukan UAT manusia. Otomasi hanya dapat memeriksa pemanggilan SpeechSynthesis, bahasa, urutan, lock input, dan cleanup.
