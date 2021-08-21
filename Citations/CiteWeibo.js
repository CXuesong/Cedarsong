// ==UserScript==
// @name         Cite Sina Weibo!
// @namespace    http://cxuesong.com/
// @version      0.1
// @description  A script making citations from Sina Weibo easier. Automatically archive cited post with archive.today.
// @author       CXuesong
// @updateURL    https://raw.githubusercontent.com/CXuesong/Cedarsong/master/Citations/CiteWeibo.js
// @homepage     https://github.com/CXuesong/Cedarsong/tree/master/Citations
// @match        https://weibo.com/*
// @match        https://archive.*/*
// @grant        GM_setClipboard
// @grant        GM_addValueChangeListener
// @grant        GM_removeValueChangeListener
// @grant        GM_setValue
// @grant        GM_deleteValue
// ==/UserScript==

(function() {
    'use strict';
    function getDate(date)
    {
        var ds = date.toISOString();
        return ds.substring(0, ds.indexOf("T"));
    }
    if (self.origin.startsWith("https://archive.")) {
        // archive.today callback.
        let correlation = location.hash.match(/CC_Correlation_(\d+)/)?.[1];
        if (correlation) {
            sessionStorage.setItem("CC.Correlation", correlation);
        } else {
            correlation = sessionStorage.getItem("CC.Correlation");
        }
        if (!correlation) return;
        let node = document.querySelector("#SHARE_LONGLINK");
        if (!node) return;
        let link = new URL(node.value);
        link.search = "";
        link = String(link);
        node = document.querySelector("time[itemprop=pubdate]");
        const date = getDate(new Date(node.getAttribute("datetime")));
        GM_setValue(`CC.Archiving.${correlation}`, {link, date});
        self.close();
        return;
    }
    async function getArchiveInfo(url) {
        const correlation = `${Date.now()}${Math.floor(Math.random()*999)}`;
        const archiveServiceUrl = `https://archive.today/?run=1&url=${encodeURIComponent(url)}#CC_Correlation_${correlation}`;
        const info = await new Promise(r => {
            window.open(archiveServiceUrl, "_blank", "resizable,scrollbars,status");
            const fieldName = `CC.Archiving.${correlation}`;
            let id = GM_addValueChangeListener(fieldName, function(name, old_value, new_value, remote) {
                console.log("Receieved value for correlation: %s: %s", correlation, new_value);
                if (!remote) return;
                GM_removeValueChangeListener(id);
                r(new_value);
                GM_deleteValue(fieldName);
            });
        });
        return info;
    }
    async function citeWeiboPost(doc) {
        let node = document.querySelector(".WB_feed_detail .WB_from a[node-type=feed_list_item_date]");
        const date = getDate(new Date(parseInt(node.getAttribute("date"))));
        node = document.querySelector(".WB_feed_detail .WB_info a[usercard]");
        const author = node.innerText;
        const title = doc.title.replace(/来自.+ - 微博\s*$/, "").trim();
        const url = (() => {
            const urlObj = new URL(location);
            urlObj.search = "";
            urlObj.hash = "";
            return String(urlObj);
        })();
        node = document.querySelector(".WB_feed_detail .WB_text[node-type=feed_list_content]")
        const quoteDom = node.cloneNode(true);
        // Remove “网页链接”
        quoteDom.querySelectorAll("a[action-type=feed_list_url]").forEach(n => n.remove());
        const quote = quoteDom.innerText.replace(/\s+/, " ").trim();
        const archive = await getArchiveInfo(url);
        const content = `<ref>{{Cite web |url=${url} |title=${title} |accessdate=${getDate(new Date())} |author=${author} `
        + `|work=新浪微博 |date=${date} |archiveurl=${archive.link} |archivedate=${archive.date} |quote=${quote}}}</ref>`;
        console.log(content);
        GM_setClipboard(content);
    }
    async function citeFeedItem(postUrl) {
        // const resp = await fetch(postUrl);
        // if (resp.url.match(/pagenotfound/)) {
        //     alert("Page not found");
        //     return;
        // }
        // const parser = new DOMParser();
        // const doc = parser.parseFromString(await resp.text(), "text/html");
        try {
            if (new URL(postUrl).pathname === location.pathname) {
                await citeWeiboPost(document);
            } else {
                const w = window.open(postUrl, "_blank", "resizable,scrollbars,status");
                await new Promise(r => w.addEventListener("load", r));
                await citeWeiboPost(w.doc);
                w.close();
            }
        } catch (err) {
            console.error(err);
        }
    }
    window.setTimeout(() => {
        const feedItems = document.querySelectorAll("[node-type=feed_list] [action-type=feed_list_item]:not([cite-weibo-processed])");
        for (const fi of feedItems) {
            fi.setAttribute("cite-weibo-processed", "yes");
            const container = fi.querySelector(".WB_from");
            const dateLink = fi.querySelector("a[node-type=feed_list_item_date]");
            const citeLink = document.createElement("a");
            const linkTarget = new URL(dateLink.href);
            linkTarget.search = "";
            citeLink.innerText = "引用";
            citeLink.href = "#";
            citeLink.addEventListener("click", e => {
                citeFeedItem(String(linkTarget));
                e.preventDefault();
                return false;
            });
            container.appendChild(citeLink);
        }
    }, 2000);
})();
