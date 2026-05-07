# GenMail Studio

GenMail Studio is a .NET 8 desktop tool (WPF + Core library) for **local username and email candidate generation** from `.txt` input files.

It is designed for internal identity migration and data normalization workflows where deterministic candidate generation, deduplication, and reporting are required.

## Lawful use cases

Use GenMail Studio only for lawful internal workflows, such as:

- Internal account migration and rename planning
- Directory/identity data normalization
- Username convention testing
- Candidate generation for internal QA/sandbox datasets

## What this app does **not** do

GenMail Studio is an offline/local generator. It:

- Does **not** send emails
- Does **not** verify emails
- Does **not** scrape or crawl websites
- Does **not** use SMTP
- Does **not** include proxy/captcha/bulk messaging features

## Safety limits

The generation pipeline includes safety controls to reduce runaway output risks:

- `MaxOutputEmails` (default: `1_000_000`)
- `MaxNumbersPerBase` (default: `1_000`)
- Input-size warning threshold (`MaxInputLinesBeforeWarning`, default: `500_000`)

If estimates exceed safety thresholds, generation is blocked.

## Build, test, publish

### Build

```bash
dotnet build GenMailStudio.sln -c Release
```

### Test

```bash
dotnet test GenMailStudio.sln -c Release
```

### Publish (Windows single-file)

```bash
dotnet publish src/GenMail.Wpf/GenMail.Wpf.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## How to use the WPF UI

1. Open the app.
2. In **Input**, choose a `.txt` file with `Browse .txt`.
3. In **Domain**, enter target domain (for example `example.com`).
4. In **Rules**, select username templates (or use Select all / Clear all / Defaults).
5. In **Number settings**, select numbering mode/placement and optional range text.
6. In **Modes**, choose dedupe mode (and alias mode if needed).
7. In **Safety settings**, set output limits.
8. Click **Estimate** to preview conservative volume.
9. Click **Start** to run; use **Cancel** to stop.
10. After completion, use **Open Folder** to inspect outputs.

## Input file format

- Plain UTF-8 text file (`.txt`)
- One input value per line
- Accepts either full names or direct usernames

Example:

```text
Nguyen Van A
Tran Thi Mai
jdoe
```

## Username rules (overview)

Rules are deterministic templates (for example `firstlast`, `first.dot.last`, `flast`, etc.).

Common token families include:

- name parts (`first`, `middle`, `last`)
- initials (`fi`, `li`, ...)
- aggregated forms (`all`, `reverseAll`)
- length-limited slices (`first3`, `last3`, ...)

Selected rules are applied per normalized input.

## Number generation (overview)

Numbering supports:

- Ranges: `0-9`, `00-99`, `001-050`, `1900-1999`
- Lists: `1,2,3,10`
- Mixed: `01-03,99`

Modes:

- `BaseOnly`
- `NumberedOnly`
- `BaseAndNumbered`

Placement:

- `SuffixOnly`
- `PrefixOnly`
- `InfixBeforeLastToken`
- `SuffixAndPrefix`
- `All`

## Dedupe modes (overview)

- `None`: no dedupe checks
- `PerRun`: in-memory dedupe for current run
- `Persistent`: SQLite-backed dedupe persisted across runs

## Output files

Generation creates an output folder named with timestamp (`yyyyMMdd_HHmmss`) containing:

- `usernames.txt`
- `emails.txt`
- `duplicate_skipped.csv`
- `quality_rejected.csv`
- `rejected_inputs.csv`
- `summary.txt`

## Troubleshooting

- **Build fails on Linux for WPF target**: ensure WPF project enables Windows targeting (`EnableWindowsTargeting=true`).
- **Input rejected**: confirm input file exists and has `.txt` extension.
- **Domain errors**: verify a domain-like value (for example `example.com`) and no `@`.
- **Too many outputs**: reduce selected rules and/or number range, or lower safety thresholds intentionally.
- **No output folder**: check Status field for error/cancel message.

## Performance notes

- Input is processed using streaming line reads (not full-file load).
- Output files are written with buffered writers.
- Large rule/range combinations can multiply output quickly; use Estimate + Safety settings before Start.
- Persistent dedupe (SQLite) trades some speed for cross-run duplicate prevention.

- For small files: keep default rules and use in-memory dedupe for best speed.
- For large files: reduce rule count/number ranges and use SQLite dedupe for cross-run protection.
- Many rules multiplied by wide number ranges can expand output dramatically.
- Use Estimate before Start to avoid unexpectedly large output volume.


## GitHub Actions Windows publish

You can produce a Windows x64 self-contained single-file package from GitHub Actions.

### Run manually

1. Open the **Actions** tab in GitHub.
2. Select **Publish Windows** workflow.
3. Click **Run workflow**.

The workflow also runs automatically when pushing a tag matching `v*` (for example `v1.0.0`).

### Download artifact

After the workflow completes:

1. Open the workflow run.
2. Download the artifact named **GenMailStudio-win-x64**.
3. Extract it and run the published executable on Windows.

### Local publish command

```bash
dotnet publish src/GenMail.Wpf/GenMail.Wpf.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./artifacts/GenMailStudio-win-x64
```
