import sys, math, collections
def ler(c):
    P=collections.defaultdict(list)
    for l in open(c):
        if not l.startswith("P|"): continue
        _,t,n,m,o,b=l.strip().split("|")
        if m.startswith("?"): m="?"
        P[(t,n,m)].append((list(map(float,o.split())),list(map(float,b.split()))))
    return P
G=ler(sys.argv[1]); U=ler(sys.argv[2])
maxo=maxb=0; ruins=[]; falta=[]
for k in sorted(set(G)|set(U)):
    g=G.get(k,[]); u=list(U.get(k,[]))
    if len(g)!=len(u): falta.append((k,len(g),len(u)))
    for (o,b) in g:
        if not u: break
        j=min(range(len(u)),key=lambda i: sum((u[i][0][a]-o[a])**2 for a in range(3)))
        uo,ub=u.pop(j)
        do=math.sqrt(sum((uo[a]-o[a])**2 for a in range(3)))
        db=max(abs(ub[a]-b[a]) for a in range(9))
        if k[2]!="?":
            maxo=max(maxo,do); maxb=max(maxb,db)
        if do>0.01 or db>0.01: ruins.append((k,do,db,o,uo))
print("peças Godot %d, Unity %d" % (sum(map(len,G.values())),sum(map(len,U.values()))))
print("diferença máx: posição %.4f m, base %.4f" % (maxo,maxb))
for f in falta: print("QUANTIDADE", f)
for r in ruins[:30]: print("LONGE", r)
