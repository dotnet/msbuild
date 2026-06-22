import sys, os, glob
from collections import defaultdict

def load_dir(d):
    # Per-file aggregation, tagged parent (has cfg_serialize) vs child (has cfg_deserialize).
    files = glob.glob(os.path.join(d, "thprofile_*.csv"))
    bytes_field = defaultdict(lambda: [0,0])   # name -> [count, total]
    phase = defaultdict(lambda: [0,0.0])       # name -> [count, total_ms]
    for f in files:
        with open(f) as fh:
            lines = fh.read().splitlines()
        rows = []
        roles = set()
        for ln in lines[1:]:
            p = ln.split(',')
            if len(p) < 4: continue
            rows.append(p)
            if p[0]=='MS' and p[1] in ('cfg_serialize','cfg_deserialize','res_serialize','res_deserialize'):
                roles.add(p[1])
        is_parent = 'cfg_serialize' in roles
        is_child  = 'cfg_deserialize' in roles
        for p in rows:
            kind,name,cnt,tot = p[0],p[1],int(p[2]),float(p[3])
            if kind=='BYTES':
                # cfg_* bytes only from parent; res_* bytes only from child (avoid double count)
                if name.startswith('cfg_') and not is_parent: continue
                if name.startswith('res_') and not is_child:  continue
                bytes_field[name][0]+=cnt; bytes_field[name][1]+=int(tot)
            else:
                phase[name][0]+=cnt; phase[name][1]+=tot
    return bytes_field, phase

def fmt_mb(b): return f"{b/1024/1024:,.1f} MB"

for d in sys.argv[1:]:
    bf, ph = load_dir(d)
    print("="*70)
    print(os.path.basename(d))
    print("="*70)

    print("\n--- TaskHostConfiguration content (bytes sent parent->child) ---")
    tot = bf.get('cfg_total',[0,0])
    n = tot[0]
    print(f"{'field':<16}{'count':>8}{'total':>14}{'avg/cfg':>12}{'% of cfg':>10}")
    for k in ['cfg_total','cfg_env','cfg_taskParams','cfg_globalProps','cfg_warnings','cfg_other']:
        c,t = bf.get(k,[0,0])
        pct = (100.0*t/tot[1]) if tot[1] else 0
        avg = (t/c) if c else 0
        print(f"{k:<16}{c:>8}{fmt_mb(t):>14}{avg:>10,.0f}B{pct:>9.1f}%")

    dedup = bf.get('cfg_env',[0,0])[1] + bf.get('cfg_globalProps',[0,0])[1] + bf.get('cfg_warnings',[0,0])[1]
    print(f"\n  dedup-able (env+globalProps+warnings) = {fmt_mb(dedup)}  = {100.0*dedup/tot[1]:.1f}% of config bytes" if tot[1] else "")

    print("\n--- TaskHostTaskComplete content (bytes child->parent) ---")
    rtot = bf.get('res_total',[0,0])
    print(f"{'field':<16}{'count':>8}{'total':>14}{'avg/res':>12}{'% of res':>10}")
    for k in ['res_total','res_env','res_outputs','res_other']:
        c,t = bf.get(k,[0,0])
        pct = (100.0*t/rtot[1]) if rtot[1] else 0
        avg = (t/c) if c else 0
        print(f"{k:<16}{c:>8}{fmt_mb(t):>14}{avg:>10,.0f}B{pct:>9.1f}%")

    print("\n--- CPU/wall time by phase (summed across all task hosts) ---")
    order = ['cfg_serialize','cfg_deserialize','cfg_env_send','cfg_env_recv',
             'res_serialize','res_deserialize','res_env_send','res_env_recv',
             'env_apply','param_set','execute','output_get','env_capture_restore']
    print(f"{'phase':<22}{'count':>8}{'total_ms':>14}{'avg_ms':>10}")
    exec_ms = ph.get('execute',[0,0.0])[1]
    for k in order:
        c,t = ph.get(k,[0,0.0])
        avg = (t/c) if c else 0
        print(f"{k:<22}{c:>8}{t:>14,.0f}{avg:>10.3f}")

    # Overhead phases (everything except execute), summed
    ovh = sum(ph.get(k,[0,0.0])[1] for k in ['cfg_serialize','cfg_deserialize','res_serialize','res_deserialize','env_apply','param_set','output_get','env_capture_restore'])
    print(f"\n  execute (real task work)          = {exec_ms:,.0f} ms")
    print(f"  measured overhead phases (summed) = {ovh:,.0f} ms")
    if exec_ms:
        print(f"  overhead/execute ratio            = {ovh/exec_ms:.2f}x")
    print(f"  deserialize/receive (cfg+res)     = {ph.get('cfg_deserialize',[0,0.0])[1]+ph.get('res_deserialize',[0,0.0])[1]:,.0f} ms")
    # Dedup-able env transfer time (receive side carries the transfer cost)
    env_recv = ph.get('cfg_env_recv',[0,0.0])[1] + ph.get('res_env_recv',[0,0.0])[1]
    print(f"  ENV transfer time (recv side, cfg+res) = {env_recv:,.0f} ms  <-- removable by dedup")
    print()

