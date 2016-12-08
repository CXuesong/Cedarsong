// ==UserScript==
// @name         Cite BlogClans!
// @namespace    http://cxuesong.com/
// @version      0.1
// @description  A script making citations from BlogClans easier.
// @author       CXuesong
// @match        http://blogclan.katecary.co.uk/*
// @match        http://erinhunter.katecary.co.uk/*
// @match        http://warriorswish.net/*
// @match        https://web.archive.org/*/http://erinhunter.katecary.co.uk/*
// @grant        GM_setClipboard
// @require      https://code.jquery.com/jquery-2.1.4.min.js
// ==/UserScript==

(function() {
    'use strict';
    function GetDate(date)
    {
        var ds = date.toISOString();
        return ds.substring(0, ds.indexOf("T"));
    }
    var WpDateMatcher = /.*?(?=\s*at)/i;
    // Strip date form [DATE] at [TIME] expression
    function StripWpDate(expr)
    {
        var m = expr.match(WpDateMatcher);
        if (m)
        {
            var d = m[0];
            return NormalizeDate(d);
        }
        else return null;
    }
    function NormalizeDate(dateExpr)
    {
        if (!dateExpr) return null;
        dateExpr = dateExpr.trim();
        if (dateExpr.length > 0) return GetDate(new Date(dateExpr));
        return null;
    }
    function Cleanup(expr)
    {
        return expr.replace(/\s+/, " ").trim();
    }
    // For ErinHunter.KateCary
    function WpLegacyParser(commentBody)
    {
        return {
            author : $(".comment-author cite", commentBody).text().trim(),
            date : StripWpDate($(".comment-meta", commentBody).text().trim()),
            anchor : commentBody.id.replace(/^div-/, ""),
        };
    }
    // For BlogClan.KateCary and others
    function WpParser(commentBody)
    {
        return {
            author : $(".comment-author", commentBody).text().trim(),
            date : StripWpDate($(".comment-meta", commentBody).text().trim()),
            anchor : commentBody.id,
        };
    }
    function WpArticleParser()
    {
        return {
            title : Cleanup($(".entry-title").text()),
            author : Cleanup($("article .author").text()),
            date :  NormalizeDate($("article time").text()),
            anchor : null,
        };
    }
    function WpLegacyArticleParser()
    {
        return {
            title : Cleanup($(".entry-title").text()),
            author : Cleanup($(".entry .entry-author-link").text()),
            date :  NormalizeDate($(".entry .entry-date").text()),
            anchor : null,
        };
    }
    // Get Base URL
    var baseUrl = window.location.href;
    var anchorPos = baseUrl.indexOf("#");
    if (anchorPos >= 0) baseUrl = baseUrl.substring(0, anchorPos);
    var now = GetDate(new Date());
    // Decide the Parser
    var articleParser = WpArticleParser;
    var commentParser = WpParser;
    if (baseUrl.match(/erinhunter\.katecary.co.uk/))
    {
        articleParser = WpLegacyArticleParser;
        commentParser = WpLegacyParser;
    }
    ////////// Article //////////
    var data = articleParser();
    data.work = "BlogClan";
    var citeButton = $('<a class="btn btn-default" href="#">引用文章</a>');
    citeButton.attr("refdata", "<ref>{{Cite web |url=" + baseUrl + " |title=" + data.title + " |accessdate=" + now + " |author=" + data.author + " |work=" + data.work + " |date=" + (data.date || "") + " |quote= }}</ref>");
    citeButton.click(function (e) {
        GM_setClipboard($(this).attr("refdata"), "text");
    });
    $(".entry-meta", this).append(citeButton);
    ////////// Comments //////////
    // Decide the title
    var title = Cleanup($("#comments-title").text());
    $(".comment-body").each(function (index) {
        var data = commentParser(this);
        data.work = "BlogClan";
        var citeButton = $('<a class="btn btn-default">引用评论</a>');
        var commentUrl = baseUrl + "#" + data.anchor;
        citeButton.click(function (e) {
            GM_setClipboard($(this).attr("refdata"), "text");
        });
        citeButton.attr("href", "#" + data.anchor);
        citeButton.attr("refdata", "<ref>{{Cite web |url=" + commentUrl + " |title=" + title + " |accessdate=" + now + " |author=" + data.author + " |work=" + data.work + " |date=" + (data.date || "") + " |quote= }}</ref>");
        var panel = $(".reply", this);
        if (panel) panel.append(citeButton);
        else $(".comment-text", this).after(citeButton);
    });
})();