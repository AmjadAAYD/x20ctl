"""Builds the Linux Studio's opening film from the Windows app's intro.mp4.

The Linux app can't count on a video codec being installed (distros differ, and H.264 is missing from some by
default), so it plays the same film as standalone WebP stills, which it decodes itself with the Skia it already ships:

    desktop-dotnet/X20Ctl.Linux/Brand/intro-film.frames   a zip (stored, not compressed): film.json + f0000.webp ...
    desktop-dotnet/X20Ctl.Linux/Brand/intro-film.wav      the film's own sound, 48 kHz 16-bit stereo PCM

Every still stands alone, so a slow machine skips frames instead of falling behind the sound.

Needs an ffmpeg with libwebp: the one on PATH, or `pip install imageio-ffmpeg`.
Run from the repository root:  python tools/linux-studio/build_intro_film.py
"""
import json
import pathlib
import shutil
import subprocess
import sys
import tempfile
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = ROOT / "desktop-dotnet" / "X20Ctl.Product" / "Brand" / "intro.mp4"
OUT = ROOT / "desktop-dotnet" / "X20Ctl.Linux" / "Brand"
FPS, WIDTH, HEIGHT, QUALITY = 60, 1600, 900, 80


def ffmpeg() -> str:
    found = shutil.which("ffmpeg")
    if found:
        return found
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except ImportError:
        sys.exit("ffmpeg not found: put it on PATH or `pip install imageio-ffmpeg`")


def main() -> None:
    tool = ffmpeg()
    OUT.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as temp:
        frames = pathlib.Path(temp)
        subprocess.run([tool, "-hide_banner", "-loglevel", "error", "-y", "-i", str(SOURCE),
                        "-vf", f"fps={FPS},scale={WIDTH}:{HEIGHT}:flags=lanczos,format=yuv420p",
                        "-c:v", "libwebp", "-q:v", str(QUALITY), str(frames / "f%04d.webp")], check=True)
        stills = sorted(frames.glob("f*.webp"))
        if not stills:
            sys.exit("ffmpeg wrote no frames (does it have libwebp?)")
        meta = {"source": "desktop-dotnet/X20Ctl.Product/Brand/intro.mp4", "fps": FPS, "width": WIDTH, "height": HEIGHT, "frames": len(stills)}
        with zipfile.ZipFile(OUT / "intro-film.frames", "w", zipfile.ZIP_STORED) as pack:
            pack.writestr(zipfile.ZipInfo("film.json", (2026, 10, 19, 0, 0, 0)), json.dumps(meta, indent=2))
            for i, still in enumerate(stills):
                pack.write(still, f"f{i:04d}.webp")
    subprocess.run([tool, "-hide_banner", "-loglevel", "error", "-y", "-i", str(SOURCE), "-vn",
                    "-c:a", "pcm_s16le", "-ar", "48000", "-ac", "2", str(OUT / "intro-film.wav")], check=True)
    print(f"{len(stills)} frames at {FPS} fps, {WIDTH}x{HEIGHT}: {(OUT / 'intro-film.frames').stat().st_size / 1e6:.1f} MB; "
          f"sound {(OUT / 'intro-film.wav').stat().st_size / 1e6:.1f} MB")


if __name__ == "__main__":
    main()
