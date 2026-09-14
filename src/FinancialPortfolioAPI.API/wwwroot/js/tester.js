"use strict";
const $ = id => document.getElementById(id);
let apiKey = "", offset = 0, total = 0, selected = null, txOffset = 0, txTotal = 0, busy = false;
const limit = 12, txLimit = 10;
const money = n => new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(n);
function node(tag, text, className) { const e = document.createElement(tag); if (text !== undefined) e.textContent = text; if (className) e.className = className; return e; }
function notice(text, error = false) { $("notice").textContent = text; $("notice").classList.toggle("error", error); }
function button(text, action, className = "secondary") { const b = node("button", text, className); b.type = "button"; b.onclick = () => run(action); return b; }
async function request(path, method = "GET", body) {
    const response = await fetch("/api/v1" + path, {
        method, headers: { "X-Api-Key": apiKey, ...(body ? { "Content-Type": "application/json" } : {}) },
        ...(body ? { body: JSON.stringify(body) } : {})
    });
    const text = await response.text();
    let data; try { data = JSON.parse(text); } catch { data = { title: text || response.statusText }; }
    $("request-info").textContent = method + " /api/v1" + path + " → " + response.status;
    $("response-json").textContent = JSON.stringify(data, null, 2);
    if (!response.ok) {
        let message = Object.values(data.errors || {}).flat().join(" ") || data.title || "Request failed.";
        if (response.status === 409) message += " Refresh the portfolio before retrying.";
        throw new Error(message);
    }
    return data;
}
async function run(action) {
    if (busy) return;
    busy = true;
    const buttons = [...document.querySelectorAll("button")];
    const previous = buttons.map(b => b.disabled);
    buttons.forEach(b => b.disabled = true);
    document.querySelectorAll(".form-error").forEach(e => e.textContent = "");
    try { await action(); }
    catch (e) {
        notice(e.message, true);
        if ($("create-dialog").open) $("create-form").querySelector(".form-error").textContent = e.message;
        if ($("detail-dialog").open) $("detail-error").textContent = e.message;
    }
    finally {
        buttons.forEach((b, i) => b.disabled = previous[i]);
        busy = false;
        ["refresh", "new-portfolio"].forEach(id => $(id).disabled = !apiKey);
        $("previous").disabled = !apiKey || offset === 0;
        $("next").disabled = !apiKey || offset + limit >= total;
        $("tx-previous").disabled = txOffset === 0;
        $("tx-next").disabled = txOffset + txLimit >= txTotal;
    }
}
async function loadPortfolios() {
    notice("Loading portfolios…");
    let result = await request("/portfolio?offset=" + offset + "&limit=" + limit);
    if (offset > 0 && result.items.length === 0) { offset = Math.max(0, offset - limit); result = await request("/portfolio?offset=" + offset + "&limit=" + limit); }
    total = result.total;
    $("portfolios").replaceChildren();
    if (!result.items.length) $("portfolios").append(node("div", "No portfolios yet. Create one to get started.", "empty card"));
    for (const p of result.items) {
        const card = node("article", undefined, "card portfolio");
        card.append(node("h3", p.name));
        const meta = node("div", undefined, "meta"); meta.append(node("span", p.clientName), node("span", p.status, "badge"));
        card.append(meta, node("div", money(p.totalValue), "value"));
        const info = node("div", undefined, "meta"); info.append(node("span", "Cash " + money(p.cashBalance)), node("span", p.holdings.length + " holdings"));
        card.append(info, node("p", p.riskProfile + " risk · Unrealized " + money(p.unrealizedGainLoss)));
        card.append(button("View portfolio", async () => { txOffset = 0; await loadDetail(p.id); $("detail-dialog").showModal(); }, ""));
        $("portfolios").append(card);
    }
    $("page-info").textContent = total ? (offset + 1) + "–" + Math.min(offset + limit, total) + " of " + total : "0 portfolios";
    notice("Connected · " + total + " portfolio" + (total === 1 ? "" : "s"));
}
async function loadDetail(id) {
    selected = await request("/portfolio/" + id);
    const p = selected;
    $("detail-title").textContent = p.name;
    $("detail-meta").textContent = p.clientName + " · " + p.clientId + " · " + p.status + " · Version " + p.version;
    $("metrics").replaceChildren();
    for (const [label, value] of [["Total value", p.totalValue], ["Cash balance", p.cashBalance], ["Unrealized gain / loss", p.unrealizedGainLoss]]) {
        const item = node("div", undefined, "metric"); item.append(node("span", label), node("strong", money(value))); $("metrics").append(item);
    }
    $("holdings").replaceChildren();
    for (const h of p.holdings) {
        const row = node("tr");
        for (const value of [h.symbol, h.shares, money(h.currentPrice), money(h.marketValue), money(h.gainLoss)]) row.append(node("td", value));
        $("holdings").append(row);
    }
    if (!p.holdings.length) { const cell = node("td", "No holdings. Settle a buy transaction to add one."); cell.colSpan = 5; const row = node("tr"); row.append(cell); $("holdings").append(row); }
    $("edit-name").value = p.name; $("edit-risk").value = p.riskProfile; $("edit-status").value = p.status;
    if (!$("reference").value) $("reference").value = "ui-" + crypto.randomUUID();
    await loadTransactions();
}
async function loadTransactions() {
    const result = await request("/transaction/portfolio/" + selected.id + "?offset=" + txOffset + "&limit=" + txLimit);
    txTotal = result.total;
    $("transactions").replaceChildren();
    for (const t of result.items) {
        const item = node("div", undefined, "transaction");
        const heading = node("div", undefined, "inline"); heading.append(node("strong", t.type + (t.symbol ? " · " + t.symbol : "") + " · " + money(t.netAmount)), node("span", t.status, "badge"));
        item.append(heading, node("p", t.referenceNumber + " · Version " + t.version));
        const actions = node("div", undefined, "inline");
        const transitions = t.status === "Pending" ? ["Executed", "Cancelled", "Failed"] : t.status === "Executed" ? ["Settled", "Failed"] : [];
        for (const status of transitions) actions.append(button(({ Executed: "Execute", Cancelled: "Cancel", Failed: "Mark failed", Settled: "Settle" })[status], async () => {
            await request("/transaction/" + t.id + "/status", "PATCH", { expectedVersion: t.version, status });
            await loadDetail(selected.id); await loadPortfolios(); notice("Transaction " + status.toLowerCase() + ".");
        }));
        item.append(actions); $("transactions").append(item);
    }
    if (!result.items.length) $("transactions").append(node("p", "No transactions yet."));
    $("tx-page-info").textContent = txTotal ? (txOffset + 1) + "–" + Math.min(txOffset + txLimit, txTotal) + " of " + txTotal : "0 transactions";
}
$("connect-form").onsubmit = e => { e.preventDefault(); run(async () => { apiKey = $("api-key").value.trim(); offset = 0; await loadPortfolios(); $("api-key").value = ""; }); };
$("disconnect").onclick = () => { if (busy) return; apiKey = ""; selected = null; total = 0; $("api-key").value = ""; $("portfolios").replaceChildren(node("div", "Disconnected. Enter an API key to reconnect.", "empty card")); $("response-json").textContent = ""; $("request-info").textContent = "No requests yet."; $("page-info").textContent = ""; ["refresh", "new-portfolio", "previous", "next"].forEach(id => $(id).disabled = true); notice("Disconnected."); };
$("refresh").onclick = () => run(loadPortfolios);
$("previous").onclick = () => run(async () => { offset -= limit; await loadPortfolios(); });
$("next").onclick = () => run(async () => { offset += limit; await loadPortfolios(); });
$("tx-previous").onclick = () => run(async () => { txOffset -= txLimit; await loadTransactions(); });
$("tx-next").onclick = () => run(async () => { txOffset += txLimit; await loadTransactions(); });
$("new-portfolio").onclick = () => { $("create-form").reset(); $("create-form").querySelector(".form-error").textContent = ""; $("create-dialog").showModal(); };
document.querySelectorAll("[data-close]").forEach(b => b.onclick = () => $(b.dataset.close).close());
document.querySelectorAll("dialog").forEach(d => d.addEventListener("cancel", e => { if (busy) e.preventDefault(); }));
$("create-form").onsubmit = e => { e.preventDefault(); run(async () => {
    const p = await request("/portfolio", "POST", { name: $("portfolio-name").value.trim(), clientId: $("client-id").value.trim(), clientName: $("client-name").value.trim(), riskProfile: $("risk-profile").value });
    $("create-dialog").close(); await loadPortfolios(); txOffset = 0; await loadDetail(p.id); $("detail-dialog").showModal(); notice("Portfolio created. Add and settle a deposit to fund it.");
}); };
$("edit-form").onsubmit = e => { e.preventDefault(); run(async () => {
    await request("/portfolio/" + selected.id, "PUT", { expectedVersion: selected.version, name: $("edit-name").value.trim(), riskProfile: $("edit-risk").value, status: $("edit-status").value });
    await loadDetail(selected.id); await loadPortfolios(); notice("Portfolio updated.");
}); };
$("recalculate").onclick = () => run(async () => {
    await request("/portfolio/" + selected.id + "/recalculate", "POST", { expectedVersion: selected.version });
    await loadDetail(selected.id); await loadPortfolios(); notice("Demo prices refreshed.");
});
$("delete").onclick = () => run(async () => {
    if (!confirm("Soft-delete this portfolio? The API will reject deletion if cash, holdings or outstanding transactions remain.")) return;
    await request("/portfolio/" + selected.id + "?expectedVersion=" + selected.version, "DELETE");
    $("detail-dialog").close(); selected = null; await loadPortfolios(); notice("Portfolio deleted. Its audit and transaction history are retained.");
});
function tradeFields() {
    const trade = ["Buy", "Sell"].includes($("transaction-type").value);
    $("cash-fields").hidden = trade; $("trade-fields").hidden = !trade; $("amount").disabled = trade;
    ["shares", "price", "commission", "symbol"].forEach(id => { $(id).disabled = !trade; });
    ["shares", "price", "commission"].forEach(id => { $(id).required = trade; });
}
$("transaction-type").onchange = tradeFields; tradeFields();
$("transaction-form").onsubmit = e => { e.preventDefault(); run(async () => {
    const type = $("transaction-type").value;
    const body = { type, referenceNumber: $("reference").value.trim() };
    if (["Buy", "Sell"].includes(type)) Object.assign(body, { symbol: $("symbol").value, shares: $("shares").value, price: $("price").value, commission: $("commission").value });
    else body.amount = $("amount").value;
    await request("/transaction/portfolio/" + selected.id, "POST", body);
    $("reference").value = "ui-" + crypto.randomUUID();
    // Transactions are oldest first, so navigate to the final page after creating one.
    const count = await request("/transaction/portfolio/" + selected.id + "?limit=1");
    txOffset = Math.floor(Math.max(0, count.total - 1) / txLimit) * txLimit;
    await loadTransactions(); notice("Pending transaction created. Execute, then settle to update balances.");
}); };
