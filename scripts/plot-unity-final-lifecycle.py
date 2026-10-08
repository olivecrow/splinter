"""Render current UF-01 costs and clearly dated RB comparisons; never run Unity.

Reads the preserved public datasets. Requires matplotlib; an existing optional
installation at .local/readme-plot-runtime is also recognized. Original RB/UR
charts remain unchanged for their historical reports.
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

data = json.loads((ROOT / 'reports/benchmarks/unity-final-lifecycle-20261008.json').read_text(encoding='utf-8'))
rows = data['originalComparison']
plt.rcParams.update({'font.family': 'DejaVu Sans', 'font.size': 11, 'svg.fonttype': 'none', 'text.color': '#243043', 'axes.labelcolor': '#243043'})
fig, axes = plt.subplots(1, 2, figsize=(13.8, 5.6), gridspec_kw={'width_ratios': [1.1, 1]})
fig.subplots_adjust(left=.13, right=.94, top=.71, bottom=.23, wspace=.54)
colors = ['#8090A6', '#237C66']

def elapsed(seconds):
    return f'{int(seconds//60)}m {seconds%60:04.1f}s'

for ax, keys, labels, title in [
    (axes[0], ['checkpointA', 'releaseA', 'checkpointB', 'releaseB'], ['A checkpoint', 'A release', 'B checkpoint', 'B release'], 'Individual commands'),
    (axes[1], ['preserveRelease', 'libraryRestore'], ['A/B preserve\n+ release', 'Restore original\nLibrary'], 'Separate wall-clock intervals'),
]:
    largest = max(rows[key]['originalSeconds']/60 for key in keys)
    for i, (key, label) in enumerate(zip(keys, labels)):
        row = rows[key]
        for j, field in enumerate(['originalSeconds', 'currentSeconds']):
            y = i*1.25 + (j-.5)*.32
            value = row[field]/60
            ax.barh(y, value, height=.29, color=colors[j], label=['Original RB', 'Current UF'][j] if i==0 else None)
            ax.text(value + largest*.018, y, elapsed(row[field]), va='center', fontsize=9, weight='bold' if j else 'normal')
    ax.set_yticks([i*1.25 for i in range(len(keys))], labels)
    ax.invert_yaxis()
    ax.set_xlim(0, largest*1.30)
    ax.set_xlabel('Minutes; linear scale from zero')
    ax.set_title(title, loc='left', fontsize=13, weight='bold', pad=15)
    ax.grid(axis='x', color='#E5E8ED', linewidth=.8)
    ax.set_axisbelow(True)
    ax.tick_params(axis='y', length=0, pad=9)
    for side in ('top', 'right', 'left'):
        ax.spines[side].set_visible(False)
    ax.spines['bottom'].set_color('#C4CBD4')

fig.text(.045, .94, 'Same Unity A/B edits, after lifecycle optimization', fontsize=20, weight='bold')
fig.text(.045, .875, f"Preserve + release: {elapsed(rows['preserveRelease']['originalSeconds'])} → {elapsed(rows['preserveRelease']['currentSeconds'])}", fontsize=15, color=colors[1])
fig.legend(*axes[0].get_legend_handles_labels(), loc='upper right', bbox_to_anchor=(.97, .895), ncol=2, frameon=False)
fig.text(.045, .12, 'Unity 6000.3.22f1 · Same 1,602 source files and 3.14 GB Library · One cohort per candidate; caches not cleared.', fontsize=10)
fig.text(.045, .075, 'Original preservation overlapped tasks and included diagnosis/recovery; current tasks preserve/release sequentially.', fontsize=10)
fig.text(.045, .03, 'Editor preparation and shutdown are excluded. Individual command times overlap in RB; do not add them to the wall-clock bars.', fontsize=10)
for extension in ('png', 'svg'):
    fig.savefig(ROOT / f'docs/images/unity-final-lifecycle.{extension}', dpi=180, facecolor='white')
plt.close(fig)

# Current-facing README charts. Codex/storage values remain dated RB evidence.
historical = json.loads((ROOT / 'reports/benchmarks/unity-workspace-comparison-20261007.json').read_text(encoding='utf-8'))
timing = data['timingSeconds']
codex = historical['timing']['codex']
assert data['candidateCommit'].startswith('116fd44')
assert data['original']['commit'] == 'b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1'
assert data['closure']['libraryRestored'] and data['closure']['ownedEditorsAndProvidersAbsent']
current_color, baseline_color = '#237C66', '#4A6894'

def duration(seconds):
    return elapsed(seconds) if seconds >= 60 else f'{seconds:.1f}s'

def style_current(ax, xlabel):
    ax.set_xlabel(xlabel, labelpad=9)
    ax.grid(axis='x', color='#E5E8ED', linewidth=.8)
    ax.set_axisbelow(True)
    ax.tick_params(axis='y', length=0, pad=10)
    for side in ('top', 'right', 'left'):
        ax.spines[side].set_visible(False)
    ax.spines['bottom'].set_color('#C4CBD4')

def export_current(fig, name):
    for extension in ('png', 'svg'):
        fig.savefig(ROOT / f'docs/images/{name}.{extension}', dpi=180, facecolor='white')
    plt.close(fig)

stages = [
    ('Prepare Library basis', timing['libraryPrepare']),
    ('Create A + B workspaces', timing['ensureA'] + timing['ensureB']),
    ('Editors to compiled readiness', timing['initial']),
    ('Edit to recompiled readiness', timing['edit']),
    ('Inspect selected assets', timing['assetInspection']),
    ('Normal Editor shutdown', timing['stop']),
    ('A/B checkpoint + release', timing['preserveRelease']),
    ('Restore original Library', timing['libraryRestore']),
]
fig, ax = plt.subplots(figsize=(12.4, 6.2))
fig.subplots_adjust(left=.29, right=.94, bottom=.26, top=.76)
values = [seconds / 60 for _, seconds in stages]
ax.barh(range(len(stages)), values, color=current_color, height=.58)
ax.set_yticks(range(len(stages)), [label for label, _ in stages])
ax.invert_yaxis()
ax.set_xlim(0, max(values)*1.28)
style_current(ax, 'Elapsed minutes; linear scale from zero')
for index, (_, seconds) in enumerate(stages):
    ax.text(seconds/60 + .08, index, duration(seconds), va='center', weight='bold')
fig.text(.045, .94, 'Current Splinter workflow: ' + duration(timing['fullLifecycle']), fontsize=21, weight='bold')
fig.text(.045, .865, 'Preparation through final restoration · Two independent Unity Editors and the original A/B edits', fontsize=12)
fig.text(.045, .125, 'UF-01 · 116fd44 · 2026-10-08 · Unity 6000.3.22f1 · 2.80 GB source + existing 3.14 GB Library', fontsize=10)
fig.text(.045, .078, 'Initial readiness and editing run concurrently across A/B; creation and checkpoint/release run A then B.', fontsize=10)
fig.text(.045, .031, 'One measured cohort; caches not cleared. Small dispatch gaps are included in the total. Both stop commands succeeded.', fontsize=10)
export_current(fig, 'unity-current-workflow')

fig, axes = plt.subplots(1, 2, figsize=(12.8, 4.8))
fig.subplots_adjust(left=.18, right=.94, top=.69, bottom=.36, wspace=.70)
for ax, current_key, old_key, title, divisor, unit in [
    (axes[0], 'initial', 'initialEditorCohortSeconds', 'Editors to compiled readiness', 60, 'Minutes'),
    (axes[1], 'edit', 'editCohortSeconds', 'One-line edits to compiled readiness', 1, 'Seconds'),
]:
    seconds = [codex[old_key], timing[current_key]]
    values = [s/divisor for s in seconds]
    ax.barh([1, 0], values, color=[baseline_color, current_color], height=.43)
    ax.set_yticks([1, 0], ['Codex (RB)', 'Splinter (UF)'])
    ax.set_xlim(0, max(values)*1.45)
    ax.set_title(title, loc='left', fontsize=12, pad=18, weight='bold')
    style_current(ax, unit + '; scale from zero')
    for y, value, original_seconds in zip([1, 0], values, seconds):
        ax.text(value + max(values)*.035, y, duration(original_seconds), va='center', weight='bold', fontsize=10)
fig.text(.045, .935, 'Recorded Codex baseline vs optimized Splinter', fontsize=20, weight='bold')
fig.text(.045, .845, f"Edit / recompile overhead: +{timing['edit']-codex['editCohortSeconds']:.1f}s for Splinter", fontsize=14)
setup = timing['libraryPrepare'] + timing['ensureA'] + timing['ensureB']
fig.text(.045, .19, f'Editor intervals start after workspace creation. Separate Splinter Library/workspace setup: {duration(setup)}.', fontsize=10)
fig.text(.045, .13, 'Codex creates new Libraries; Splinter references the existing original Library. Same source and A/B edits.', fontsize=10)
fig.text(.045, .07, 'Codex: RB, 2026-10-07 (not rerun) · Splinter: UF, 116fd44, 2026-10-08 · One cohort; caches not cleared.', fontsize=10)
fig.text(.045, .018, 'Complete Codex creation/preservation timings are unavailable; this is not an end-to-end speedup comparison.', fontsize=10)
export_current(fig, 'unity-current-editor-comparison')

allocation = [historical['allocation'][method]['additionalAllocatedBytes'] for method in ('codex', 'splinter')]
values = [value / 2**30 for value in allocation]
reduction = 100*(1-allocation[1]/allocation[0])
fig, ax = plt.subplots(figsize=(11.6, 4.7))
fig.subplots_adjust(left=.23, right=.93, top=.70, bottom=.34)
ax.barh([1, 0], values, color=[baseline_color, current_color], height=.44)
ax.set_yticks([1, 0], ['Codex (RB)', 'Splinter (RB)'])
ax.set_xlim(0, max(values)*1.2)
style_current(ax, 'Additional file allocation (GiB)')
for y, value in zip([1, 0], values):
    ax.text(value + .15, y, f'{value:.2f} GiB', va='center', weight='bold')
fig.text(.045, .94, f'Recorded storage saving: {reduction:.2f}%', fontsize=21, weight='bold')
fig.text(.045, .845, 'Historical RB measurement · 2026-10-07 · Storage was not remeasured in UF', fontsize=13)
fig.text(.045, .17, 'Two workspaces after editing and compilation. Task files and owned/shared metadata counted once.', fontsize=10)
fig.text(.045, .10, 'Original source, original Library payload and shared Git objects excluded. This is stage allocation, not peak disk use.', fontsize=10)
fig.text(.045, .035, 'The earlier candidate recorded these values; they are not an allocation measurement of optimized 116fd44.', fontsize=10)
export_current(fig, 'unity-recorded-storage')
