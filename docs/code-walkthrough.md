# Panduan Alur Kode SiapSD Cognitive

## Struktur project

- `Domain` menyimpan model inti, misalnya `Question`, `AssessmentSession`, dan `AssessmentResponse`.
- `Application` menyimpan aturan penilaian, pemilihan soal, dan `WorkingMemoryEngine`.
- `Infrastructure` menyimpan EF Core, PostgreSQL, seed data, dan `AssessmentService`.
- `API` menerima request HTTP dan mengubah validasi input menjadi respons yang aman.
- `siap-sd-cognitive-web` adalah React frontend untuk presentasi stimulus dan pengumpulan jawaban.

## Alur assessment

`Frontend -> API -> AssessmentService -> PostgreSQL -> scoring -> report`.

Frontend mengirim jawaban dan waktu respons. API memanggil `AssessmentService`. Service memilih cara penilaian berdasarkan tipe soal, menyimpan snapshot soal dan telemetry ke PostgreSQL. Laporan membaca respons dan skor domain yang tersimpan.

## SubmitAnswer: scalar dan sequence

Soal biasa memakai jawaban scalar, contohnya `B` atau `5`. Soal Working Memory memakai JSON array, misalnya `["apel","mobil","ikan"]`, karena urutan merupakan bagian dari jawaban.

Backend menentukan benar/salah. Frontend tidak menghitung correctness agar jawaban tidak dapat dimanipulasi di browser. Payload dengan bentuk salah ditolak sebagai HTTP 400 pada batas API sebelum respons disimpan.

## Working Memory

Frontend bergerak melalui fase `ready`, `presenting`, `retentionGap`, `responding`, `submitting`, dan `complete`.

Stimulus ditampilkan atau dibacakan terlebih dahulu, lalu disembunyikan. Timer respons baru mulai saat fase `responding`, sehingga durasi presentasi tidak dihitung sebagai waktu berpikir. Server menghitung posisi benar, akurasi posisi, dan jenis kesalahan. Ketepatan sebagian berguna untuk laporan, tetapi tidak menaikkan span adaptif; dua trial penuh benar diperlukan sebelum span naik.

## File penting

- `Application/WorkingMemory.cs`: engine penilaian urutan dan metrik span.
- `Infrastructure/AssessmentService.cs`: memvalidasi, menilai, dan menyimpan respons.
- `Infrastructure/WorkingMemorySeedData.cs`: tugas Working Memory asli dan idempoten.
- `API/Program.cs`: endpoint assessment dan pemetaan validasi ke HTTP 400.
- `web/src/WorkingMemoryPlayer.tsx`: presentasi stimulus, speech `id-ID`, dan input urutan.
