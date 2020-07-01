$(document).ready(function () {  

    function doSearch() {
        var button = $('#search-button');
        function onSearchComplete() {
            button.prop('disabled', false);
            button.html('Search');
        }

        button.prop('disabled', true);
        button.html(`<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Searching...`);
        var query = $('#search-query').val();
        $.ajax({  
            type: "GET",  
            url: "/api/runfo/jobs/" + definition + "?query=" + query,  
            contentType: "application/json",
            dataType: "json",  
            success: function (data) {  
                var ctx = document.getElementById('search-chart').getContext('2d');
                data.sort((l, r) => -(l.failed - r.failed));
                data = data.slice(0, 20);

                let labels = data.map(x => x.jobName);
                let values = data.map(x => x.failed);
                let chartColors = [
                    'rgb(255, 99, 132)', // red
                    'rgb(255, 159, 64)', // orange
                    'rgb(255, 205, 86)', // yellow
                    'rgb(75, 192, 192)', // green
                    'rgb(54, 162, 235)', // blue
                    'rgb(153, 102, 255)', // purple
                ];
                let background = data.map((_, i) => chartColors[i % chartColors.length]);

                var myBarChart = new Chart(ctx, {
                    type: 'horizontalBar',
                    data: {
                        labels: labels,
                        datasets: [{
                            label: 'Fail Count',
                            data: values,
                            backgroundColor: background
                        }]
                    },
                    options: {
                        scales: {
                            yAxes: [{
                                ticks: {
                                    beginAtZero: true
                                }
                            }]
                        }
                    }
                });
                onSearchComplete();
            },
            failure: function (data) {  
                alert(data.responseText);  
                onSearchComplete();
            },
            error: function (data) {  
                alert(data.responseText);  
                onSearchComplete();
            }
        });
    }

    $('#search-button').click(function(e) {
        e.preventDefault();
        doSearch();
    });  

    doSearch();
});