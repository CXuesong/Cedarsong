// ==UserScript==
// @name         Cite Sina Weibo!
// @namespace    http://cxuesong.com/
// @version      0.2
// @description  A script making citations from Sina Weibo easier. Automatically archive cited post with archive.today.
// @author       CXuesong
// @updateURL    https://raw.githubusercontent.com/CXuesong/Cedarsong/master/Citations/CiteWeibo.js
// @homepage     https://github.com/CXuesong/Cedarsong/tree/master/Citations
// @match        https://weibo.com/*
// @match        https://archive.ph/*
// @grant        GM_setClipboard
// @grant        GM_addValueChangeListener
// @grant        GM_removeValueChangeListener
// @grant        GM_setValue
// @grant        GM_deleteValue
// @grant        window.opener
// @grant        window.postMessage
// @grant        window.ommessage
// @grant        window.close
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
        if (!window.opener) return;
        let node = document.querySelector("#SHARE_LONGLINK");
        if (!node) return;
        let link = new URL(node.value);
        link.search = "";
        link = String(link);
        node = document.querySelector("time[itemprop=pubdate]");
        const date = getDate(new Date(node.getAttribute("datetime")));
        window.opener.postMessage({type: "CCArchive", link, date}, "*");
        window.close();
        return;
    }
    async function getArchiveInfo(url) {
        const correlation = `${Date.now()}${Math.floor(Math.random()*999)}`;
        const archiveServiceUrl = `https://archive.today/?run=1&url=${encodeURIComponent(url)}`;
        const info = await new Promise((r, rej) => {
            const w = window.open(archiveServiceUrl, "_blank", "resizable,scrollbars,status");
            if (!w) {
                alert("请允许弹出窗口。");
                rej(new Error("Popup window blocked."));
            }
            function onGuestMessage(e) {
                const { data } = e;
                if (e.source === w && data && typeof data === "object" && data.type === "CCArchive") {
                    r(data);
                    e.stopImmediatePropagation();
                    window.removeEventListener("message", onGuestMessage);
                }
            }
            window.addEventListener("message", onGuestMessage);
        });
        return info;
    }
    async function citeWeiboPost(doc) {
        let node = doc.querySelector(".WB_feed_detail .WB_from a[node-type=feed_list_item_date]");
        const date = getDate(new Date(parseInt(node.getAttribute("date"))));
        node = doc.querySelector(".WB_feed_detail .WB_info a[usercard]");
        const author = node.innerText;
        const title = doc.title.replace(/来自.+ - 微博\s*$/, "").trim();
        const url = (() => {
            const urlObj = new URL(doc.location);
            urlObj.search = "";
            urlObj.hash = "";
            return String(urlObj);
        })();
        node = doc.querySelector(".WB_feed_detail .WB_text[node-type=feed_list_content]")
        const quoteDom = node.cloneNode(true);
        // Remove “网页链接”
        quoteDom.querySelectorAll("a[action-type=feed_list_url]").forEach(n => n.remove());
        const quote = quoteDom.innerText.replace(/\s+/ug, " ").trim();
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
        if (new URL(postUrl).pathname === location.pathname) {
            await citeWeiboPost(document);
        } else {
            const w = window.open(postUrl, "_blank", "resizable,scrollbars,status");
            await new Promise((r, rej) => {
                if (!w) {
                    alert("请允许弹出窗口。");
                    rej(new Error("Popup window blocked."));
                }
                w.$$CC_onDomReady = r;
            });
            await citeWeiboPost(w.document);
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
        if (unsafeWindow.$$CC_onDomReady) {
            unsafeWindow.$$CC_onDomReady();
            window.close();
        }
    }, 2000);
})();
