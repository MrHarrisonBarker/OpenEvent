import { sleep, check, group } from "k6";
import http from "k6/http";
import {
    htmlReport
} from "https://raw.githubusercontent.com/benc-uk/k6-reporter/main/dist/bundle.js";

export const options = {
  stages: [
    { duration: "1m", target: 10 },
    { duration: "3m", target: 10 },
    { duration: "1m", target: 0 },
  ]
};

export default function main() {
  let response;

  group("Search", function () {
    response = http.get("http://localhost:5000/search", {
      headers: {
        accept:
          "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9",
        "accept-encoding": "gzip, deflate, br",
        "accept-language": "en-US,en;q=0.9,pt;q=0.8",
        "cache-control": "max-age=0",
        connection: "keep-alive",
        dnt: "1",
        host: "localhost:5000",
        "sec-fetch-dest": "document",
        "sec-fetch-mode": "navigate",
        "sec-fetch-site": "same-origin",
        "sec-fetch-user": "?1",
        "upgrade-insecure-requests": "1",
      },
    });
    check(response, {
      "status equals 200": response => response.status.toString() === "200",
    });
  });

  // Automatically added sleep
  sleep(1);
}

export function handleSummary(data) {
    return {
        "LoadSearch.html": htmlReport(data),
    };
}