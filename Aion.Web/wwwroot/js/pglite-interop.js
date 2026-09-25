const instances = {};

export async function create(name) {
    if (instances[name]) return;

    const { PGlite } = await import('https://cdn.jsdelivr.net/npm/@electric-sql/pglite/dist/index.js');
    instances[name] = await PGlite.create(`idb://${name}`);
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

// db.transaction holds PGlite's transaction lock, so no other query can interleave with this one,
// and the explicit rollback discards anything the statement changed. Returns the first column of
// each row as text.
export async function queryRolledBack(name, sql) {
    const db = instances[name];
    if (!db) throw new Error(`Database '${name}' not found`);

    return await db.transaction(async (tx) => {
        const result = await tx.query(sql);
        await tx.rollback();
        const column = result.fields[0]?.name;
        return column === undefined ? [] : result.rows.map(row => String(row[column]));
    });
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

export async function destroy(name) {
    const db = instances[name];
    if (db) {
        await db.close();
        delete instances[name];
    }

    const databases = await indexedDB.databases();
    const pgliteDbNames = databases
        .map(db => db.name)
        .filter(n => n && n.includes(name));

    for (const dbName of pgliteDbNames) {
        indexedDB.deleteDatabase(dbName);
    }
}
