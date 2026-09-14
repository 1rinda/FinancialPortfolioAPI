"use strict";
const $ = id => document.getElementById(id);
let operations = [], spec;
const examples = {
    CreatePortfolioRequest: { name: "Demo portfolio", clientId: "CL001", clientName: "Demo Client", riskProfile: "Moderate" },
    UpdatePortfolioRequest: { expectedVersion: 1, name: "Updated portfolio", riskProfile: "Moderate", status: "Active" },
    VersionRequest: { expectedVersion: 1 },
    CreateTransactionRequest: { type: "Deposit", referenceNumber: "deposit-001", amount: 10000 },
    StatusRequest: { expectedVersion: 1, status: "Executed" }
};
function choose() {
    const op = operations[$("endpoint").value];
    $("description").textContent = op.definition.summary;
    const query = (op.definition.parameters || []).filter(p => p.in === "query" && p.required).map(p => p.name + "=1");
    $("path").value = op.path + (query.length ? "?" + query.join("&") : "");
    const schema = op.definition.requestBody?.content?.["application/json"]?.schema?.$ref?.split("/").pop();
    $("body").value = schema ? JSON.stringify(examples[schema] || {}, null, 2) : "";
    $("body").disabled = !schema;
}
async function initialize() {
    try {
        const response = await fetch("/openapi.json");
        if (!response.ok) throw new Error("Cannot load OpenAPI specification.");
        spec = await response.json();
        for (const [path, methods] of Object.entries(spec.paths)) for (const [method, definition] of Object.entries(methods)) {
            if (!["get", "post", "put", "patch", "delete"].includes(method)) continue;
            const option = document.createElement("option"); option.value = operations.length; option.textContent = method.toUpperCase() + " " + path;
            operations.push({ path, method: method.toUpperCase(), definition }); $("endpoint").append(option);
        }
        choose();
    } catch (e) { $("result-status").textContent = e.message; $("send").disabled = true; }
}
$("endpoint").onchange = choose;
$("explorer").onsubmit = async e => {
    e.preventDefault(); $("send").disabled = true;
    try {
        const op = operations[$("endpoint").value], path = $("path").value;
        const url = new URL(path, location.origin);
        if (url.origin !== location.origin || !url.pathname.startsWith("/api/v1/") || path.includes("{")) throw new Error("Use a local /api/v1/ path and replace all {id} placeholders.");
        const body = $("body").disabled ? undefined : JSON.stringify(JSON.parse($("body").value));
        const response = await fetch(url, { method: op.method, headers: { "X-Api-Key": $("key").value, ...(body ? { "Content-Type": "application/json" } : {}) }, ...(body ? { body } : {}) });
        const text = await response.text(); let output = text;
        try { output = JSON.stringify(JSON.parse(text), null, 2); } catch {}
        $("result-status").textContent = op.method + " " + url.pathname + " → HTTP " + response.status;
        $("result").textContent = output;
    } catch (error) { $("result-status").textContent = error.message; }
    finally { $("send").disabled = false; }
};
initialize();
