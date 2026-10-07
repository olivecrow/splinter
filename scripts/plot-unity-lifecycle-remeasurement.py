"""Render recorded Unity rerun timings; never execute the benchmark.

Matplotlib is optional. Reuse the documentation-only local runtime if present.
"""
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
runtime = ROOT / '.local/readme-plot-runtime'
if runtime.exists():
    sys.path.insert(0, str(runtime))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

data = json.loads((ROOT / 'reports/benchmarks/unity-lifecycle-remeasurement-20261007.json').read_text(encoding='utf-8'))
old = data['timingSeconds']['historicalSplinter']
new = data['timingSeconds']['currentSplinter']
codex = data['timingSeconds']['historicalCodex']
rows = [('initial', 'Editor launch to compiled readiness'),
        ('edit', 'One-line edits to compiled readiness'),
        ('preserveRelease', 'A/B checkpoint + release sequence')]
plt.rcParams.update({'font.family': 'DejaVu Sans', 'font.size': 11, 'svg.fonttype': 'none', 'text.color': '#243043', 'axes.labelcolor': '#243043'})
fig, axes = plt.subplots(3, 1, figsize=(10.5, 8.8))
fig.subplots_adjust(left=.23, right=.87, top=.84, bottom=.18, hspace=.8)
fig.text(.06, .96, 'Same Unity project, current Splinter lifecycle', fontsize=19, weight='bold')
fig.text(.06, .912, '1,602 source files · 42,420 Library files · independent A/B Editors\nIdentical recorded input; one cohort per candidate, profiling included.', fontsize=11, color='#536175')
for ax, (key, title) in zip(axes, rows):
    values = [old[key] / 60, new[key] / 60]
    labels = ['Splinter (earlier)', 'Splinter (current)']
    colors = ['#A8AFBA', '#297D75']
    if key in codex:
        values.insert(0, codex[key] / 60)
        labels.insert(0, 'Codex (earlier)')
        colors.insert(0, '#365F9C')
    positions = list(reversed(range(len(values))))
    ax.barh(positions, values, height=.55, color=colors)
    ax.set_yticks(positions, labels)
    ax.set_title(title, loc='left', fontsize=12, pad=8, weight='bold')
    ax.set_xlim(0, max(values) * 1.17)
    ax.set_xlabel('Minutes', labelpad=3)
    ax.grid(axis='x', color='#E5E8ED', linewidth=.8)
    ax.set_axisbelow(True)
    for spine in ax.spines.values():
        spine.set_visible(False)
    ax.tick_params(axis='y', length=0)
    for y, value in zip(positions, values):
        ax.text(value + max(values) * .02, y, f'{value:.2f}', va='center', weight='bold')
fig.text(.06, .085, 'Codex values are the earlier recorded baseline; Codex was not rerun.\nEarlier preservation includes overlap, recovery and concurrent diagnostics; current is sequential A then B.\nSetup, asset inspection and Library restoration are separate; see the full timing table.', fontsize=10, color='#536175')
out = ROOT / 'docs/images'
for ext in ('svg', 'png'):
    fig.savefig(out / f'unity-lifecycle-remeasurement.{ext}', dpi=180, facecolor='white')
plt.close(fig)
