import { Octokit } from "https://esm.sh/@octokit/rest";

(function (w, d) {
    DarkReader.auto(true);

    const targetRepo = {
        owner: "Leayal",
        repo: "SnowBreakLauncher-Dotnet"
    }, markdown = new showdown.Converter({
        strikethrough: true,
        tables: true,
        tasklists: true,
        ghCodeBlocks: true,
        ghMentions: true,
        ghMentionsLink: true,
        openLinksInNewWindow: true,
        emoji: true
    }), octokit = new Octokit({
        userAgent: "SnowBreakLauncher-Dotnet v1.0"
    }), content = d.getElementById("content");
    markdown.setFlavor('github');

    function AddLoadingElement(parentDom) {
        "use strict";
        const loader = d.createElement("div");
        const animation = d.createElement("div");
        animation.classList.add("cssload-tetrominos");
        for (let i = 1; i <= 4; i++) {
            const loader_box = d.createElement("div");
            loader_box.classList.add("cssload-tetromino", "cssload-box" + i.toString());
            animation.appendChild(loader_box);
        }
        loader.classList.add("loader3d");
        loader.appendChild(animation);
        parentDom.appendChild(loader);
    }

    function RemoveLoadingElement(parentDom) {
        "use strict";
        const loader = parentDom.querySelector(".loader3d");
        if (loader) {
            parentDom.removeChild(loader);
        }
    }

    function RenderNotFound(parentDom) {
        "use strict";
        const notFound_title = d.createElement("h2");
        notFound_title.textContent = "The document you requested doesn't exist.";
        parentDom.appendChild(notFound_title);
    }
    
    function RenderHome(parentDom) {
        "use strict";
        const home_body = d.createElement("p"), home_title = d.createElement("h2");
        home_title.textContent = "Hello there.";
        home_body.textContent = "There's nothing to describe the launcher yet. But you can visit Downloads and Changelog page.";
        parentDom.appendChild(home_title);
        parentDom.appendChild(home_body);
    }

    async function RenderDownloads(parentDom) {
        "use strict";
        AddLoadingElement(parentDom);
        const latestReleaseInfo = (await octokit.rest.repos.getLatestRelease(targetRepo)).data,
        release_body = d.createElement("p"), release_title = d.createElement("h1"), release_assets = d.createElement("div"), title_download = d.createElement("h1");
        RemoveLoadingElement(parentDom);
        
        release_title.textContent = latestReleaseInfo.name;
        release_body.innerHTML = markdown.makeHtml(latestReleaseInfo.body);
        for (const asset of latestReleaseInfo.assets) {
            const theLink = d.createElement("a");
            theLink.href = asset.browser_download_url;
            theLink.download = theLink.textContent = asset.name;
            release_assets.appendChild(theLink);
        }
        title_download.textContent = "Downloads:"

        release_title.classList.add("git-release-title");
        release_body.classList.add("git-release-body");
        release_assets.classList.add("git-release-asset-list");

        parentDom.appendChild(release_title);
        parentDom.appendChild(release_body);
        parentDom.appendChild(title_download);
        parentDom.appendChild(release_assets);
    }

    async function RenderChangelog(parentDom) {
        "use strict";
        AddLoadingElement(parentDom);
        const commitlogs = (await octokit.rest.repos.listCommits(targetRepo)).data, commit_list = d.createElement("div"), page_description = d.createElement("h1");
        RemoveLoadingElement(parentDom);
        
        for (const commitData of commitlogs) {
            const commit_item = d.createElement("div"), commit_header = d.createElement("div");
            const commit_title = d.createElement("h2"), commit_author = d.createElement("span"), commit_summary = d.createElement("p");
            const commit_viewMoreDetail = d.createElement("a");

            commit_item.classList.add("git-commit-item");
            commit_header.classList.add("git-commit-header");
            commit_viewMoreDetail.classList.add("git-commit-viewdetail");

            commit_title.textContent = commitData.commit.committer.date;
            commit_author.textContent = "by " + commitData.commit.committer.name;

            commit_viewMoreDetail.href = commitData.html_url;
            commit_viewMoreDetail.target = "_blank";
            commit_viewMoreDetail.textContent = "View more details on Github";

            commit_summary.innerHTML = markdown.makeHtml(commitData.commit.message);
            
            commit_header.appendChild(commit_title);
            commit_header.appendChild(commit_author);
            commit_header.appendChild(commit_viewMoreDetail);
            commit_item.appendChild(commit_header);
            commit_item.appendChild(commit_summary);
            commit_list.appendChild(commit_item);
        }
        
        page_description.textContent = "Below are the 30 recent commits. You can view all commits at "
        const page_description_link = d.createElement("a");
        page_description_link.href = "https://github.com/Leayal/SnowBreakLauncher-Dotnet/commits/master/";
        page_description_link.target = "_blank";
        page_description_link.textContent = "Github commit log";
        page_description.appendChild(page_description_link);

        commit_list.classList.add("git-commit-list");
        
        parentDom.appendChild(page_description);
        parentDom.appendChild(commit_list);
    }

    function clearChildNodes(element) {
        "use strict";
        while(element.firstChild) element.removeChild(element.lastChild);
    }

    function isDestinationPageSame(page1, page2) {
        "use strict";
        if (typeof (page1) === "string" && typeof (page2) === "string") return (page1.toLowerCase() === page2.toLowerCase());
        else return (page1 == page2);
    }

    function isDestinationPageSameAsCurrent(dstPage) {
        "use strict";
        const _search = new URLSearchParams(w.location.search), currentPage = _search.get("page");
        return isDestinationPageSame(dstPage, currentPage);
    }

    function navigatePage(currentpage) {
        "use strict";

        clearChildNodes(content);
        if (currentpage) {
            const comparand = currentpage.toLowerCase();
            if (comparand == "downloads") {
                RenderDownloads(content);
            } else if (comparand == "changelog") {
                RenderChangelog(content);
            } else {
                RenderNotFound;
            }    
        } else {
            RenderHome(content);
        }
    }

    const helperfunc_toggleClass = (assert, dom, ...classes) => (assert ? dom.classList.add : dom.classList.remove).call(dom.classList, classes);

    // Get our entry to start after all DOM are ready to be manipulated.
    w.addEventListener("DOMContentLoaded", function () {
        "use strict";
        const navigationItems = d.querySelectorAll(".navigation-list a");
        if (navigationItems && navigationItems.length !== 0) {
            
            const eventCallback = function (e) {
                "use strict";

                // We yeet every chance that the browser will do its default behaviors.
                e.preventDefault();
                e.stopImmediatePropagation();
                e.stopPropagation();

                // Get the qualified and normalized "destination url".
                const newUrl = new URL(e.target.href, w.location.href);

                // Search for query "?page" and use its value
                const _search = newUrl.searchParams, dstPage = _search.get("page");

                if (!isDestinationPageSameAsCurrent(dstPage)) {
                    // Get the current history state object of this window instance.
                    const currentState = w.history.state;
                    // Patch/replace the URL of the state object found above to change URL without reloading page.
                    w.history.replaceState(currentState, "eh", newUrl.href);
                    
                    navigatePage(dstPage);
                    
                    for (let i = 0; i < navigationItems.length; i++) {
                        const link = navigationItems.item(i);
                        helperfunc_toggleClass(isDestinationPageSame((new URL(link.href)).searchParams.get("page"), dstPage), link, "current-page");
                    }
                }
            };

            for (let i = 0; i < navigationItems.length; i++) {
                const link = navigationItems.item(i);
                link.addEventListener("click", eventCallback, false);
                const params = (new URL(link.href)).searchParams;
                if (isDestinationPageSameAsCurrent(params.get("page"))) {
                    link.classList.add("current-page");
                }
            }
        }

        if (content) {
            const _search = new URLSearchParams(w.location.search);
            navigatePage(_search.get("page"));
        }
    }, true);
})(window, window.document);