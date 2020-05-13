$(document).ready(function () {  
    $('#search-button').click(function(e) {
        e.preventDefault();
        var query = $('#search-text').val();
        $.ajax({  
            type: "GET",  
            url: "/api/runfo/builds?query=" + query,  
            contentType: "application/json",
            dataType: "json",  
            success: function (data) {  
                var body = $('#table-body');
                body.empty();
                $.each(data, function (i, item) {  
                    var row = '<tr>' +
                        '<td>' + item.result + '</td>' +
                        '<td><a href="' + item.buildUri + '">' + item.buildNumber + '</td>' +
                        '<tr>';
                    body.append(row);
                });
            },
            failure: function (data) {  
                alert(data.responseText);  
            },
            error: function (data) {  
                alert(data.responseText);  
            }
        });
    });  
});