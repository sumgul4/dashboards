// DevExtreme dxTreeMap — Blazor JS interop modülü
// Kullanım: DevExtremeTreeMap.razor bileşeni bu modülü import eder.
// Gereksinim: sayfada DevExtreme yüklü olmalı (bkz. OKUBENI.md)
//   <script src="_content/DevExpress.Blazor/dx.all.js"></script>  (veya CDN)

const instances = new WeakMap();

function options(element, data, dotNetRef, selected) {
    return {
        dataSource: data,          // [{ name, value, color }]
        valueField: 'value',
        labelField: 'name',
        colorizer: { type: 'none' },   // renkler dataSource'taki 'color' alanından gelir
        layoutAlgorithm: 'squarified',
        tile: {
            border: { visible: true, color: '#ffffff', width: 1 },
            label: {
                visible: true,
                font: { family: 'Archivo, Segoe UI, sans-serif', size: 12, weight: 700, color: '#ffffff' },
                wordWrap: 'none',
                textOverflow: 'ellipsis'
            },
            selectionStyle: { border: { color: '#0e2a4d', width: 3 } },
            hoverStyle: { border: { color: '#ffffff', width: 2 } }
        },
        group: { label: { visible: false } },
        interactWithGroup: false,
        selectionMode: 'single',
        tooltip: {
            enabled: true,
            font: { family: 'Archivo, Segoe UI, sans-serif', size: 12 },
            customizeTooltip(arg) {
                return { text: `${arg.node.label()}: ${arg.value.toLocaleString('tr-TR')}` };
            }
        },
        onClick(e) {
            const label = e.node.label();
            e.node.select(!e.node.isSelected());
            dotNetRef.invokeMethodAsync('OnCellClick', label);
        },
        onDrawn(e) {
            if (!selected) return;
            e.component.getRootNode().eachChild(n => { if (n.label() === selected) n.select(true); });
        },
        size: { width: element.clientWidth, height: element.clientHeight }
    };
}

export function create(element, data, dotNetRef, selected) {
    if (!window.DevExpress?.viz?.dxTreeMap) {
        console.error('DevExtreme (dx.all.js) yüklenmemiş — dxTreeMap oluşturulamadı.');
        return;
    }
    const widget = new DevExpress.viz.dxTreeMap(element, options(element, data, dotNetRef, selected));
    const ro = new ResizeObserver(() => widget.render());
    ro.observe(element);
    instances.set(element, { widget, ro, dotNetRef });
}

export function update(element, data, selected) {
    const inst = instances.get(element);
    if (!inst) return;
    inst.widget.option('dataSource', data);
    inst.widget.clearSelection();
    if (selected) {
        inst.widget.getRootNode().eachChild(n => { if (n.label() === selected) n.select(true); });
    }
}

export function dispose(element) {
    const inst = instances.get(element);
    if (!inst) return;
    inst.ro.disconnect();
    inst.widget.dispose();
    instances.delete(element);
}
