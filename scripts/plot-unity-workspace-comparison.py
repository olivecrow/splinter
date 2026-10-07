"""Render the reviewed README benchmark dataset as static PNG and SVG charts.

Requires matplotlib; an optional local install at .local/readme-plot-runtime is
also recognized. This renders recorded measurements and never runs Unity.
"""
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
local_runtime = ROOT / '.local/readme-plot-runtime'
if local_runtime.exists():
    sys.path.insert(0, str(local_runtime))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.ticker import MaxNLocator

data = json.loads((ROOT / 'reports/benchmarks/unity-workspace-comparison-20261007.json').read_text(encoding='utf-8'))
output = ROOT / 'docs/images'
output.mkdir(exist_ok=True)
colors = ['#365F9C', '#CB702D']
labels = ['Codex worktrees', 'Splinter + Library basis']
plt.rcParams.update({'font.family': 'DejaVu Sans', 'font.size': 12, 'svg.fonttype': 'none', 'axes.labelcolor': '#243043', 'text.color': '#243043', 'axes.edgecolor': '#C4CBD4'})

def style(ax, label):
    ax.set_xlabel(label, labelpad=10)
    ax.set_xlim(left=0)
    ax.xaxis.set_major_locator(MaxNLocator(5))
    ax.grid(axis='x', color='#E5E8ED', linewidth=.8)
    ax.set_axisbelow(True)
    for name in ('top', 'right', 'left'):
        ax.spines[name].set_visible(False)
    ax.tick_params(axis='y', length=0, pad=10)

def export(fig, name):
    for ext in ('svg', 'png'):
        fig.savefig(output / f'{name}.{ext}', dpi=180, facecolor='white')
    plt.close(fig)

alloc = [data['allocation'][m]['additionalAllocatedBytes'] for m in ('codex', 'splinter')]
gib = [n / 2**30 for n in alloc]
reduction = 100 * (1 - alloc[1] / alloc[0])
fig, ax = plt.subplots(figsize=(10.6, 4.3))
fig.subplots_adjust(left=.27, right=.86, bottom=.27, top=.76)
ax.barh([1, 0], gib, color=colors, height=.46)
ax.set_yticks([1, 0], labels)
style(ax, 'Additional file allocation (GiB)')
ax.set_xlim(0, max(gib) * 1.14)
for y, v in zip([1, 0], gib):
    ax.text(v + max(gib)*.02, y, f'{v:.2f} GiB', va='center', weight='bold')
fig.text(.05, .91, 'Two independent Unity workspaces', fontsize=19, weight='bold')
fig.text(.05, .82, f'{reduction:.2f}% less additional file allocation with Splinter', fontsize=14)
fig.text(.05, .08, 'Edited and compiled; two Editors per method. Original source, shared Git objects and original Library excluded.', fontsize=10)
fig.text(.05, .025, 'Windows / NTFS · Unity 6000.3.22f1 · 2026-10-07 · One cohort per method; file allocation is not whole-disk peak.', fontsize=10)
export(fig, 'unity-workspace-storage')

fig, axes = plt.subplots(1, 2, figsize=(12, 4.8))
fig.subplots_adjust(left=.20, right=.94, bottom=.28, top=.70, wspace=.74)
stages = [('initialEditorCohortSeconds', 'Editors to compiled readiness', 'Minutes', 60), ('editCohortSeconds', 'Source edit to compiled readiness', 'Seconds', 1)]
for ax, (key, title, unit, divisor) in zip(axes, stages):
    values = [data['timing'][m][key]/divisor for m in ('codex', 'splinter')]
    ax.barh([1, 0], values, color=colors, height=.44)
    ax.set_yticks([1, 0], ['Codex', 'Splinter'])
    style(ax, unit)
    ax.set_title(title, loc='left', fontsize=12, pad=19)
    ax.set_xlim(0, max(values)*1.28)
    for y, v in zip([1, 0], values):
        ax.text(v+max(values)*.035,y,f'{v:.1f}',va='center',weight='bold')
fig.text(.05,.92,'Observed time for the same A/B code task',fontsize=19,weight='bold')
delta = data['timing']['splinter']['editCohortSeconds']-data['timing']['codex']['editCohortSeconds']
fig.text(.05,.82,f'Edit / recompile difference: {delta:+.1f} seconds for Splinter',fontsize=14)
prep=data['timing']['splinter']['libraryPreparationSeconds']
ensure=data['timing']['splinter']['workspaceEnsureCohortSeconds']
fig.text(.05,.12,f'Separate Splinter setup: Library preparation {prep/60:.2f} min once; two workspace ensure calls {ensure/60:.2f} min.',fontsize=10)
fig.text(.05,.065,'Editor bars start with workspaces already created. Codex builds new Libraries; Splinter reuses the prepared Library.',fontsize=10)
fig.text(.05,.02,'One run, Codex then Splinter; OS caches were not cleared. Includes launch/refresh polling; excludes setup and charting.',fontsize=10)
export(fig,'unity-workspace-time')

# These intervals have different boundaries from the two common Editor phases.
# Codex's complete app-creation/preservation intervals were unavailable.
splinter = data['timing']['splinter']
if 'preserveReleaseCohortSeconds' in splinter and 'libraryRestorationSeconds' in splinter:
    stage_labels = ['Prepare Library once', 'Create two workspaces', 'Preserve / release A+B', 'Restore original Library']
    values = [splinter[key]/60 for key in ('libraryPreparationSeconds', 'workspaceEnsureCohortSeconds', 'preserveReleaseCohortSeconds', 'libraryRestorationSeconds')]
    positions = list(range(3, -1, -1))
    fig, ax = plt.subplots(figsize=(12, 5.1))
    fig.subplots_adjust(left=.29, right=.91, bottom=.27, top=.76)
    ax.barh(positions, values, color=colors[1], height=.52)
    ax.set_yticks(positions, stage_labels)
    style(ax, 'Observed wall time (minutes)')
    ax.set_xlim(0, max(values)*1.22)
    for y, value in zip(positions, values):
        ax.text(value+max(values)*.02, y, f'{value:.1f}', va='center', weight='bold')
    fig.text(.05,.91,'Additional Splinter lifecycle costs',fontsize=19,weight='bold')
    fig.text(.05,.83,'Separate intervals outside the Editor comparison; comparable Codex totals unavailable',fontsize=12)
    fig.text(.05,.12,'One recorded A/B sequence. Preservation overlaps B source commit/checkpoint with A checks/release.',fontsize=10)
    fig.text(.05,.07,'The preservation bar includes A checkpoint; overlapping individual calls must not be added again.',fontsize=10)
    fig.text(.05,.025,'Normal shutdown is separate: the stop observation interval was an upper bound after CLI timeouts.',fontsize=10)
    export(fig,'unity-workspace-lifecycle')
