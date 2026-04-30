const instances = {};

export async function create(name) {
    if (instances[name]) return;

    const { PGlite } = await import('https://cdn.jsdelivr.net/npm/@electric-sql/pglite/dist/index.js');
    instances[name] = await PGlite.create();
}

export async function query(name, sql) {
    const db = instances[name];
    if (!db) throw new Error(`Database '${name}' not found`);

    const result = await db.query(sql);

    return {
        columns: result.fields.map(f => f.name),
        rows: result.rows.map(row => {
            const mapped = {};
            result.fields.forEach(f => {
                const val = row[f.name];
                mapped[f.name] = val === undefined || val === null ? null : val;
            });
            return mapped;
        }),
        affectedRows: result.affectedRows || 0
    };
}

export async function exec(name, sql) {
    const db = instances[name];
    if (!db) throw new Error(`Database '${name}' not found`);
    await db.exec(sql);
}

export function listDatabases() {
    return Object.keys(instances);
}

export async function close(name) {
    const db = instances[name];
    if (db) {
        await db.close();
        delete instances[name];
    }
}
