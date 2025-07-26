// Cache DOM selectors and configurations
const CONFIG = {
    swaggerReadyTimeout: 2000,   // Max time to wait for Swagger UI (ms)
    debounceDelay: 200,          // Debounce delay for search (ms)
    darkModeStorageKey: 'swaggerDarkMode',
    autoThemeClass: 'auto-theme'
};

// Initialize when document is loaded
document.addEventListener('DOMContentLoaded', initializeSwaggerExtensions);

// Main initialization function
function initializeSwaggerExtensions() {
    // Use a timeout to ensure we don't wait indefinitely
    let attempts = 0;
    const maxAttempts = 20; // ~4 seconds with 200ms intervals
    
    const checkSwaggerReady = setInterval(() => {
        const swaggerUI = document.querySelector('.swagger-ui');
        const informationContainer = document.querySelector('.information-container .main');
        
        if (swaggerUI && informationContainer) {
            clearInterval(checkSwaggerReady);
            console.log('Swagger UI detected, initializing custom features...');
            
            // Use requestAnimationFrame to optimize browser rendering
            requestAnimationFrame(() => {
                // Create a document fragment to minimize DOM operations
                const fragment = document.createDocumentFragment();
                
                // Create UI components and attach to fragment
                const searchContainer = createSearchContainer(fragment);
                
                // Add fragment to DOM in one operation
                informationContainer.appendChild(fragment);
                
                // Initialize features
                initDarkModeToggle();
                
                // Initialize event handlers after a delay to avoid page load performance impact
                setTimeout(() => {
                    initializeEventHandlers(searchContainer);
                }, 100);
            });
        } else if (++attempts >= maxAttempts) {
            clearInterval(checkSwaggerReady);
            console.warn('Swagger UI not found after timeout, abandoning initialization.');
        }
    }, 200);
}

// Create search container with all child elements
function createSearchContainer(parentFragment) {
    // Create and style the search container
    const searchContainer = document.createElement('div');
    searchContainer.className = 'custom-search-container';
    
    // Apply base styles
    Object.assign(searchContainer.style, {
        margin: '20px 0',
        padding: '15px',
        backgroundColor: '#f8fafc',
        borderRadius: '8px',
        boxShadow: '0 2px 4px rgba(0, 0, 0, 0.1)',
        transition: 'background-color 0.3s ease'
    });

    // Add heading
    const heading = document.createElement('h3');
    heading.textContent = 'API Explorer';
    Object.assign(heading.style, {
        margin: '0 0 15px 0',
        fontSize: '18px',
        fontWeight: '700'
    });
    searchContainer.appendChild(heading);

    // Create search wrapper
    const { searchWrapper, searchInput } = createSearchInput();
    searchContainer.appendChild(searchWrapper);

    // Create filter section
    const filterSection = document.createElement('div');
    filterSection.style.marginTop = '15px';

    const filterHeading = document.createElement('h4');
    filterHeading.textContent = 'Filter by Tag';
    Object.assign(filterHeading.style, {
        margin: '0 0 10px 0',
        fontSize: '15px',
        fontWeight: '600'
    });
    filterSection.appendChild(filterHeading);

    // Create "Select All" checkbox
    const { selectAllDiv, selectAllCheckbox } = createSelectAllCheckbox();
    filterSection.appendChild(selectAllDiv);

    // Create checkbox container
    const checkboxContainer = document.createElement('div');
    checkboxContainer.id = 'checkboxContainer';
    Object.assign(checkboxContainer.style, {
        display: 'flex',
        flexWrap: 'wrap',
        gap: '8px'
    });
    
    // Populate with tag checkboxes
    populateTagCheckboxes(checkboxContainer, selectAllCheckbox);
    filterSection.appendChild(checkboxContainer);

    // Add filter section
    searchContainer.appendChild(filterSection);
    
    // Add to parent fragment
    parentFragment.appendChild(searchContainer);
    
    // Create and add "No Results" message
    const noResultsMessage = document.createElement('div');
    noResultsMessage.id = 'noResultsMessage';
    Object.assign(noResultsMessage.style, {
        padding: '15px',
        textAlign: 'center',
        color: '#64748b',
        fontSize: '15px',
        margin: '20px 0',
        backgroundColor: '#f1f5f9',
        borderRadius: '6px',
        display: 'none'
    });
    
    // Get first opblock tag if it exists
    const firstOpblockTag = document.querySelector('.opblock-tag');
    if (firstOpblockTag && firstOpblockTag.parentElement) {
        firstOpblockTag.parentElement.insertBefore(noResultsMessage, firstOpblockTag);
    } else {
        // Fallback if tag sections aren't found yet
        parentFragment.appendChild(noResultsMessage);
    }
    
    return searchContainer;
}

// Create search input with icon
function createSearchInput() {
    const searchWrapper = document.createElement('div');
    searchWrapper.style.position = 'relative';
    searchWrapper.style.marginBottom = '15px';

    const searchIcon = document.createElement('span');
    searchIcon.innerHTML = `
        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <circle cx="11" cy="11" r="8"></circle>
            <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
        </svg>
    `;
    Object.assign(searchIcon.style, {
        position: 'absolute',
        left: '12px',
        top: '50%',
        transform: 'translateY(-50%)',
        color: '#64748b'
    });

    const searchInput = document.createElement('input');
    searchInput.id = 'apiSearch';
    searchInput.type = 'text';
    searchInput.placeholder = 'Search APIs...';
    Object.assign(searchInput.style, {
        width: '100%',
        padding: '10px 10px 10px 40px',
        border: '1px solid #e2e8f0',
        borderRadius: '6px',
        fontSize: '14px',
        backgroundColor: '#ffffff',
        transition: 'all 0.2s ease',
        boxSizing: 'border-box'
    });

    searchWrapper.appendChild(searchIcon);
    searchWrapper.appendChild(searchInput);

    return { searchWrapper, searchInput };
}

// Create "Select All" checkbox
function createSelectAllCheckbox() {
    const selectAllDiv = document.createElement('div');
    selectAllDiv.style.margin = '0 0 10px 0';
    selectAllDiv.style.width = '100%';

    const selectAllLabel = document.createElement('label');
    Object.assign(selectAllLabel.style, {
        display: 'flex',
        alignItems: 'center',
        cursor: 'pointer'
    });

    const selectAllCheckbox = document.createElement('input');
    selectAllCheckbox.type = 'checkbox';
    selectAllCheckbox.id = 'selectAll';
    selectAllCheckbox.style.margin = '0 8px 0 0';
    selectAllCheckbox.checked = true;

    const selectAllText = document.createTextNode('Select All');
    selectAllLabel.appendChild(selectAllCheckbox);
    selectAllLabel.appendChild(selectAllText);
    selectAllDiv.appendChild(selectAllLabel);
    
    return { selectAllDiv, selectAllCheckbox };
}

// Initialize event handlers for search and filters
function initializeEventHandlers(searchContainer) {
    const searchInput = document.getElementById('apiSearch');
    const checkboxContainer = document.getElementById('checkboxContainer');
    const selectAllCheckbox = document.getElementById('selectAll');
    
    if (searchInput) {
        // Use passive event listener for better performance
        searchInput.addEventListener('input', debounce(filterContent, CONFIG.debounceDelay), { passive: true });
        
        // Add focus and hover effects
        searchInput.addEventListener('focus', () => {
            Object.assign(searchInput.style, {
                borderColor: '#3b82f6',
                boxShadow: '0 0 0 3px rgba(59, 130, 246, 0.25)'
            });
        }, { passive: true });

        searchInput.addEventListener('blur', () => {
            Object.assign(searchInput.style, {
                borderColor: '#e2e8f0',
                boxShadow: 'none'
            });
        }, { passive: true });
    }
    
    if (checkboxContainer) {
        checkboxContainer.addEventListener('change', filterContent, { passive: true });
    }
    
    if (selectAllCheckbox) {
        selectAllCheckbox.addEventListener('change', toggleAllCheckboxes, { passive: true });
    }
    
    // Update UI for dark mode
    updateSearchUIForDarkMode();
}

// Initialize dark mode toggle
function initDarkModeToggle() {
    const authWrapper = document.querySelector('.auth-wrapper');

    if (!authWrapper) {
        console.warn('Cannot find .auth-wrapper element to add the dark mode toggle.');
        return;
    }

    // Create toggle container
    const toggleDiv = document.createElement('div');
    Object.assign(toggleDiv.style, {
        display: 'flex',
        alignItems: 'center',
        marginLeft: '15px'
    });

    // Create toggle components
    const { toggleSwitch, checkbox, sliderCircle, icon } = createToggleSwitch();
    
    // Create label text
    const labelText = document.createElement('span');
    labelText.textContent = 'Dark Mode';
    Object.assign(labelText.style, {
        fontSize: '14px',
        fontWeight: '500'
    });

    // Build the toggle structure
    toggleSwitch.appendChild(checkbox);
    toggleDiv.appendChild(toggleSwitch);
    toggleDiv.appendChild(labelText);
    
    // Add to DOM
    authWrapper.prepend(toggleDiv);

    // Load saved preference from localStorage
    const isDarkMode = localStorage.getItem(CONFIG.darkModeStorageKey) === 'true';
    checkbox.checked = isDarkMode;

    // Apply dark mode if saved
    if (isDarkMode) {
        document.querySelector('.swagger-ui').classList.add('dark-mode');
        sliderCircle.style.transform = 'translateX(20px)';
        icon.innerHTML = '🌙';
        updateSearchUIForDarkMode(true);
    }

    // Add event listener to toggle button
    checkbox.addEventListener('change', () => {
        const isDarkMode = checkbox.checked;
        const swaggerUI = document.querySelector('.swagger-ui');

        // Save preference to localStorage
        localStorage.setItem(CONFIG.darkModeStorageKey, isDarkMode);

        // Apply/remove dark mode class
        if (isDarkMode) {
            swaggerUI.classList.add('dark-mode');
            sliderCircle.style.transform = 'translateX(20px)';
            icon.innerHTML = '🌙';
        } else {
            swaggerUI.classList.remove('dark-mode');
            swaggerUI.classList.remove(CONFIG.autoThemeClass);
            sliderCircle.style.transform = 'translateX(0)';
            icon.innerHTML = '☀️';
        }

        // Update search UI for dark mode
        updateSearchUIForDarkMode(isDarkMode);
    }, { passive: true });
}

// Create toggle switch components
function createToggleSwitch() {
    // Create toggle switch
    const toggleSwitch = document.createElement('label');
    toggleSwitch.className = 'dark-mode-switch';
    Object.assign(toggleSwitch.style, {
        position: 'relative',
        display: 'inline-block',
        width: '44px',
        height: '24px',
        marginRight: '8px'
    });

    // Create checkbox
    const checkbox = document.createElement('input');
    checkbox.type = 'checkbox';
    checkbox.id = 'darkModeToggle';
    Object.assign(checkbox.style, {
        opacity: '0',
        width: '0',
        height: '0'
    });

    // Create slider
    const slider = document.createElement('span');
    Object.assign(slider.style, {
        position: 'absolute',
        cursor: 'pointer',
        top: '0',
        left: '0',
        right: '0',
        bottom: '0',
        backgroundColor: '#cbd5e1',
        borderRadius: '24px',
        transition: '0.4s'
    });

    // Create slider circle
    const sliderCircle = document.createElement('span');
    Object.assign(sliderCircle.style, {
        position: 'absolute',
        height: '18px',
        width: '18px',
        left: '3px',
        bottom: '3px',
        backgroundColor: 'white',
        borderRadius: '50%',
        transition: '0.4s'
    });

    // Add icon for dark/light mode
    const icon = document.createElement('span');
    icon.innerHTML = '☀️';
    Object.assign(icon.style, {
        position: 'absolute',
        top: '50%',
        left: '50%',
        transform: 'translate(-50%, -50%)',
        fontSize: '10px'
    });

    slider.appendChild(sliderCircle);
    slider.appendChild(icon);
    toggleSwitch.appendChild(slider);
    
    return { toggleSwitch, checkbox, sliderCircle, icon };
}

// Populate tag checkboxes
function populateTagCheckboxes(container, selectAllCheckbox) {
    if (!container) return;
    
    const tags = document.getElementsByClassName('opblock-tag-section');
    const uniqueTags = new Set();

    // Extract unique tags
    for (let i = 0; i < tags.length; i++) {
        const tagElement = tags[i].querySelector('[data-tag]');
        if (tagElement) {
            const tag = tagElement.getAttribute('data-tag');
            uniqueTags.add(tag);
        }
    }

    // Use documentFragment to minimize DOM operations
    const fragment = document.createDocumentFragment();

    // Create checkbox for each tag
    uniqueTags.forEach(tag => {
        const checkboxDiv = document.createElement('div');
        Object.assign(checkboxDiv.style, {
            backgroundColor: '#f1f5f9',
            padding: '6px 10px',
            borderRadius: '4px',
            display: 'inline-flex',
            alignItems: 'center',
            transition: 'background-color 0.2s ease',
            cursor: 'pointer'
        });

        const checkbox = document.createElement('input');
        checkbox.type = 'checkbox';
        checkbox.value = tag;
        checkbox.id = `tag-${tag}`;
        checkbox.style.margin = '0 8px 0 0';
        checkbox.checked = true; // Check all by default

        const label = document.createElement('label');
        label.htmlFor = `tag-${tag}`;
        label.textContent = tag;
        Object.assign(label.style, {
            fontSize: '14px',
            cursor: 'pointer',
            userSelect: 'none'
        });

        checkboxDiv.appendChild(checkbox);
        checkboxDiv.appendChild(label);
        fragment.appendChild(checkboxDiv);

        // Make the entire div clickable with event delegation
        checkboxDiv.addEventListener('click', (e) => {
            if (e.target !== checkbox) {
                checkbox.checked = !checkbox.checked;
                checkbox.dispatchEvent(new Event('change', {bubbles: true}));
            }
        }, { passive: true });
    });

    // Add fragment to container in one operation
    container.appendChild(fragment);

    // Set "Select All" checkbox initial state
    if (selectAllCheckbox) {
        selectAllCheckbox.checked = true;
    }
}

// Toggle all checkboxes
function toggleAllCheckboxes(event) {
    const isChecked = event.target.checked;
    const checkboxes = document.querySelectorAll('#checkboxContainer input[type="checkbox"]');
    
    // Use for loop instead of forEach for better performance
    for (let i = 0; i < checkboxes.length; i++) {
        checkboxes[i].checked = isChecked;
    }

    // Trigger filter update
    filterContent();
}

// Filter content based on search and checkboxes
function filterContent() {
    const searchInput = document.getElementById('apiSearch');
    if (!searchInput) return;
    
    const filter = searchInput.value.toLowerCase();
    const tagSections = document.getElementsByClassName('opblock-tag-section');
    if (!tagSections.length) return;

    // Get selected tags (cache result)
    const checkedBoxes = document.querySelectorAll('#checkboxContainer input[type="checkbox"]:checked');
    const checkedTags = new Set();
    
    for (let i = 0; i < checkedBoxes.length; i++) {
        checkedTags.add(checkedBoxes[i].value.toLowerCase());
    }

    let visibleEndpointCount = 0;

    // Update Select All checkbox state
    updateSelectAllState();

    // Use requestAnimationFrame for smooth UI updates
    requestAnimationFrame(() => {
        // Loop through each tag section
        for (let i = 0; i < tagSections.length; i++) {
            const tagSection = tagSections[i];
            const tagElement = tagSection.querySelector('[data-tag]');

            if (!tagElement) continue;
            
            const tag = tagElement.getAttribute('data-tag').toLowerCase();
            const operations = tagSection.querySelectorAll('.opblock');

            // Check if tag is selected
            const tagMatches = checkedTags.has(tag);
            let sectionHasVisibleOperations = false;

            // Filter operations within this tag
            for (let j = 0; j < operations.length; j++) {
                const operation = operations[j];
                const pathElement = operation.querySelector('[data-path]');

                if (!pathElement) continue;
                
                const path = pathElement.getAttribute('data-path').toLowerCase();
                const summary = operation.querySelector('.opblock-summary-description');
                const summaryText = summary ? summary.textContent.toLowerCase() : '';

                // Check if operation matches search and tag filter
                const pathMatches = path.includes(filter);
                const summaryMatches = summaryText.includes(filter);
                const isVisible = tagMatches && (filter === '' || pathMatches || summaryMatches);

                // Update visibility
                operation.style.display = isVisible ? '' : 'none';

                if (isVisible) {
                    sectionHasVisibleOperations = true;
                    visibleEndpointCount++;
                }
            }

            // Show/hide entire tag section
            tagSection.style.display = sectionHasVisibleOperations ? '' : 'none';
        }

        // Show message if no results
        updateNoResultsMessage(visibleEndpointCount, filter);
    });
}

// Update Select All checkbox state
function updateSelectAllState() {
    const allCheckboxes = document.querySelectorAll('#checkboxContainer input[type="checkbox"]');
    const selectAllCheckbox = document.getElementById('selectAll');
    
    if (selectAllCheckbox && allCheckboxes.length > 0) {
        let allChecked = true;
        
        for (let i = 0; i < allCheckboxes.length; i++) {
            if (!allCheckboxes[i].checked) {
                allChecked = false;
                break;
            }
        }
        
        selectAllCheckbox.checked = allChecked;
    }
}

// Update no results message
function updateNoResultsMessage(visibleEndpointCount, filter) {
    const noResultsMessage = document.getElementById('noResultsMessage');
    if (!noResultsMessage) return;
    
    if (visibleEndpointCount === 0 && filter !== '') {
        noResultsMessage.style.display = 'block';
        noResultsMessage.textContent = `No endpoints found matching "${filter}"`;
    } else {
        noResultsMessage.style.display = 'none';
    }
}

// Update search UI for dark mode
function updateSearchUIForDarkMode(isDarkMode = false) {
    const searchContainer = document.querySelector('.custom-search-container');
    if (!searchContainer) return;

    const swaggerUI = document.querySelector('.swagger-ui');
    const darkModeActive = isDarkMode || (swaggerUI && swaggerUI.classList.contains('dark-mode'));

    // Cache DOM queries and batch style updates
    const searchInput = document.getElementById('apiSearch');
    const checkboxDivs = document.querySelectorAll('#checkboxContainer > div');
    const noResultsMessage = document.getElementById('noResultsMessage');
    
    if (darkModeActive) {
        // Dark mode styles
        Object.assign(searchContainer.style, {
            backgroundColor: 'var(--dark-panel, #1e293b)',
            color: 'var(--dark-text, #f1f5f9)'
        });
        
        if (searchInput) {
            Object.assign(searchInput.style, {
                backgroundColor: 'var(--dark-bg, #0f172a)',
                color: 'var(--dark-text, #f1f5f9)',
                borderColor: 'var(--dark-border, #334155)'
            });
        }
        
        for (let i = 0; i < checkboxDivs.length; i++) {
            Object.assign(checkboxDivs[i].style, {
                backgroundColor: 'var(--dark-surface, #334155)',
                color: 'var(--dark-text, #f1f5f9)'
            });
        }
        
        if (noResultsMessage) {
            Object.assign(noResultsMessage.style, {
                backgroundColor: 'var(--dark-panel, #1e293b)',
                color: 'var(--dark-secondary-text, #94a3b8)'
            });
        }
    } else {
        // Light mode styles
        Object.assign(searchContainer.style, {
            backgroundColor: '#f8fafc',
            color: 'inherit'
        });
        
        if (searchInput) {
            Object.assign(searchInput.style, {
                backgroundColor: '#ffffff',
                color: 'inherit',
                borderColor: '#e2e8f0'
            });
        }
        
        for (let i = 0; i < checkboxDivs.length; i++) {
            Object.assign(checkboxDivs[i].style, {
                backgroundColor: '#f1f5f9',
                color: 'inherit'
            });
        }
        
        if (noResultsMessage) {
            Object.assign(noResultsMessage.style, {
                backgroundColor: '#f1f5f9',
                color: '#64748b'
            });
        }
    }
}

// Debounce function to limit how often a function is called
function debounce(func, delay) {
    let timeout;
    return function() {
        const context = this;
        const args = arguments;
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(context, args), delay);
    };
}

// Add support for system preference
function checkSystemPreference() {
    const prefersDarkMode = window.matchMedia('(prefers-color-scheme: dark)').matches;
    const hasUserPreference = localStorage.getItem(CONFIG.darkModeStorageKey) !== null;
    
    if (!hasUserPreference && prefersDarkMode) {
        const swaggerUI = document.querySelector('.swagger-ui');
        if (swaggerUI) {
            swaggerUI.classList.add(CONFIG.autoThemeClass);
            updateSearchUIForDarkMode(true);
        }
    }
    
    // Listen for system preference changes
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', event => {
        const hasUserPreference = localStorage.getItem(CONFIG.darkModeStorageKey) !== null;
        if (!hasUserPreference) {
            const swaggerUI = document.querySelector('.swagger-ui');
            if (swaggerUI) {
                if (event.matches) {
                    swaggerUI.classList.add(CONFIG.autoThemeClass);
                } else {
                    swaggerUI.classList.remove(CONFIG.autoThemeClass);
                }
                updateSearchUIForDarkMode(event.matches);
            }
        }
    });
}

// Call system preference check after initialization
setTimeout(checkSystemPreference, 500);