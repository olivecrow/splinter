"""Render graph-only README comparisons from RC-01 and SE-01; requires matplotlib."""
from pathlib import Path
import json,sys
ROOT=Path(__file__).resolve().parents[1]
runtime=ROOT/'.local/readme-plot-runtime'
if runtime.exists():sys.path.insert(0,str(runtime))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
D=json.loads((ROOT/'reports/benchmarks/unity-current-comparison-20261014.json').read_text(encoding='utf-8'))
assert D['verificationId']=='RC-01' and D['combinedResults']['sameTree']
Q=json.loads((ROOT/'reports/benchmarks/unity-shared-editor-20261014.json').read_text(encoding='utf-8'))
assert Q['verificationId']=='SE-01' and Q['comparison']['sameCombinedTree']
plt.rcParams.update({'font.family':'DejaVu Sans','font.size':11,'svg.fonttype':'none','text.color':'#243043','axes.labelcolor':'#243043'})
COLORS=['#4A6894','#237C66'];LABELS=['Codex worktrees','Splinter']
REQUESTED=set(sys.argv[1:])
def duration(s):return f'{int(s//60)}m {s%60:04.1f}s' if s>=60 else f'{s:.1f}s'
def style(ax,label):
 ax.set_xlabel(label,labelpad=9);ax.grid(axis='x',color='#E5E8ED',linewidth=.8);ax.set_axisbelow(True);ax.tick_params(axis='y',length=0,pad=10)
 for side in ['top','right','left']:ax.spines[side].set_visible(False)
 ax.spines['bottom'].set_color('#C4CBD4')
def bars(ax,values,labels,axis_label):
 ax.barh([1,0],values,color=COLORS,height=.44);ax.set_yticks([1,0],LABELS);ax.set_xlim(0,max(values)*1.30);style(ax,axis_label)
 for y,value,label in zip([1,0],values,labels):ax.text(value+max(values)*.025,y,label,va='center',weight='bold')
def export(fig,name):
 if REQUESTED and name not in REQUESTED:
  plt.close(fig);return
 for ext in ['png','svg']:
  target=ROOT/f'docs/images/{name}.{ext}'
  fig.savefig(target,dpi=180,facecolor='white',bbox_inches='tight',pad_inches=.15)
  if ext=='svg':target.write_text('\n'.join(line.rstrip() for line in target.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8')
 plt.close(fig)
fig,axes=plt.subplots(1,2,figsize=(12.8,2.6));fig.subplots_adjust(left=.18,right=.98,top=.97,bottom=.27,wspace=.75)
for ax,key,divisor,label in [(axes[0],'initial',60,'Initial compiled readiness (minutes)'),(axes[1],'edit',1,'Edit to recompiled readiness (seconds)')]:
 seconds=[D['timingSeconds'][m][key] for m in ['c','s']];bars(ax,[v/divisor for v in seconds],[duration(v) for v in seconds],label)
export(fig,'unity-readme-editor-comparison')
fig,ax=plt.subplots(figsize=(11.6,2.6));fig.subplots_adjust(left=.23,right=.97,top=.97,bottom=.27)
values=[D['storage'][m]['totalAllocatedBytes']/2**30 for m in ['c','s']];bars(ax,values,[f'{v:.2f} GiB' for v in values],'Additional file allocation (GiB)');export(fig,'unity-readme-storage')
fig,ax=plt.subplots(figsize=(11.6,2.6));fig.subplots_adjust(left=.23,right=.97,top=.97,bottom=.27)
values=[D['timingSeconds'][m]['total'] for m in ['c','s']];bars(ax,[v/60 for v in values],[duration(v) for v in values],'Complete workflow (minutes)');export(fig,'unity-readme-workflow')

fig,ax=plt.subplots(figsize=(11.6,3.2));fig.subplots_adjust(left=.29,right=.97,top=.97,bottom=.22)
values=[Q['timingSeconds']['total'],D['timingSeconds']['c']['total'],D['timingSeconds']['s']['total']]
labels=['Shared Editor\n(serial validation)','Git worktrees\n(two Editors)','Splinter\n(two Editors)']
ax.barh([2,1,0],[v/60 for v in values],color=['#737B87',*COLORS],height=.48)
ax.set_yticks([2,1,0],labels);ax.set_xlim(0,max(values)/60*1.30);style(ax,'Workspace and Editor operations (minutes)')
for y,v in zip([2,1,0],values):ax.text(v/60+max(values)/60*.025,y,duration(v),va='center',weight='bold')
export(fig,'unity-readme-shared-editor')
