const instances = {};

export async function create(name) {
    if (instances[name]) return;

    const { PGlite } = await import('https://cdn.jsdelivr.net/npm/@electric-sql/pglite/dist/index.js');
    instances[name] = await PGlite.create(`idb://${name}`);
}

async function select(name, sql, options) {
    const db = instances[name];
    if (!db) throw new Error(`Database '${name}' not found`);

    const result = await db.query(sql, [], options);

    return {
        columns: result.fields.map(f => f.name),
        types: result.fields.map(f => f.dataTypeID),
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

export async function query(name, sql) {
    return await select(name, sql);
}

// Results shown in the grid keep PostgreSQL's own text for dates, times and JSON: as JS values, dates would
// cross into .NET as UTC ISO strings (shifting timestamps without a time zone and adding a time to plain dates)
// and JSON would arrive as nested objects. A bigint beyond a JS number's exact range stays text, because
// JSON cannot carry a BigInt.
const asText = value => value;
const bigint = value => {
    const n = BigInt(value);
    return n >= BigInt(Number.MIN_SAFE_INTEGER) && n <= BigInt(Number.MAX_SAFE_INTEGER) ? Number(n) : value;
};
const resultParsers = {
    20: bigint,
    114: asText,
    1082: asText,
    1083: asText,
    1114: asText,
    1184: asText,
    1266: asText,
    3802: asText
};

// Reports a failed statement as data. An error thrown across JS interop reaches .NET as text with the
// JS stack appended, losing the SQLSTATE and the position PostgreSQL reported.
export async function run(name, sql) {
    try {
        return { ...(await select(name, sql, { parsers: resultParsers })), error: null };
    } catch (e) {
        return {
            error: {
                message: e?.message ?? String(e),
                code: e?.code ?? null,
                position: e?.position ?? null,
                detail: e?.detail ?? null,
                hint: e?.hint ?? null
            }
        };
    }
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

// PGlite's idb:// filesystem keeps each database in one IndexedDB database named after its mount
// point, /pglite/<name>. Only that one may be deleted: matching by substring also deleted unrelated
// databases, including aion-storage, which holds every saved connection and query.
export function indexedDbName(name) {
    return `/pglite/${name}`;
}

export async function destroy(name) {
    const db = instances[name];
    if (db) {
        await db.close();
        delete instances[name];
    }

    await new Promise((resolve, reject) => {
        const request = indexedDB.deleteDatabase(indexedDbName(name));
        request.onsuccess = () => resolve();
        request.onerror = () => reject(request.error);
        request.onblocked = () => {
            console.warn(`Deleting PGlite database '${name}' will finish once other tabs close it.`);
            resolve();
        };
    });
}
